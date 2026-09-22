using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using SqlFluff.Ssms.Core;
using SqlFluff.Ssms.Options;
using SqlFluff.Ssms.Services;
using Task = System.Threading.Tasks.Task;

namespace SqlFluff.Ssms
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("SQLFluff for SSMS", "SQLFluff linter and formatter integration.", "1.0.0")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideOptionPage(typeof(SqlFluffOptionsPage), "SQLFluff", "General", 0, 0, true)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.ShellInitialized_string, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(PackageGuids.PackageString)]
    public sealed class SqlFluffPackage : AsyncPackage
    {
        private ErrorListService _errors;
        private LintService _lint;
        private EditorServices _editor;
        private DocumentEvents _documentEvents;
        private IVsRunningDocumentTable _rdt;
        private uint _rdtCookie;

        // Used by MEF-composed editor components (e.g. the Light Bulb suggested-actions source) that
        // have no other way to reach this package's services.
        internal static SqlFluffPackage Instance { get; private set; }

        internal LintService LintService => _lint;
        internal EditorServices EditorServices => _editor;

        internal SqlFluffSettings GetSettings()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return ((SqlFluffOptionsPage)GetDialogPage(typeof(SqlFluffOptionsPage))).ToSettings();
        }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            Instance = this;

            var componentModel = (IComponentModel)await GetServiceAsync(typeof(SComponentModel));
            _editor = new EditorServices(
                this,
                componentModel.GetService<IVsEditorAdaptersFactoryService>(),
                componentModel.GetService<ITextDocumentFactoryService>());

            _errors = new ErrorListService(this);
            _lint = new LintService(this, _editor, _errors);

            var commands = await GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commands != null)
            {
                AddCommand(commands, PackageIds.CmdLint, (s, e) => RunOnActiveDocument(EditorAction.Lint), requiresSql: true);
                AddCommand(commands, PackageIds.CmdFix, (s, e) => RunOnActiveDocument(EditorAction.Fix), requiresSql: true);
                AddCommand(commands, PackageIds.CmdFormat, (s, e) => RunOnActiveDocument(EditorAction.Format), requiresSql: true);
                AddCommand(commands, PackageIds.CmdClear, (s, e) => ClearActiveDocument(), requiresSql: true);
                AddCommand(commands, PackageIds.CmdOptions, (s, e) => ShowOptionPage(typeof(SqlFluffOptionsPage)), requiresSql: false);
            }

            _rdt = (IVsRunningDocumentTable)await GetServiceAsync(typeof(SVsRunningDocumentTable));
            _documentEvents = new DocumentEvents(this, _rdt, _editor, _lint);
            _rdt.AdviseRunningDocTableEvents(_documentEvents, out _rdtCookie);
            _documentEvents.AttachToOpenDocuments();

            // Non-blocking: warn once if sqlfluff isn't reachable, instead of waiting for the first Lint/Fix to fail.
            JoinableTaskFactory.RunAsync(CheckSqlFluffAvailabilityAsync).Task.FileAndForget("sqlfluff/availability-check");
        }

        private async Task CheckSqlFluffAvailabilityAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();
            SqlFluffSettings settings = GetSettings();

            string error = await Task.Run(() => SqlFluffRunner.CheckAvailabilityAsync(settings, CancellationToken.None));
            if (error == null)
            {
                return;
            }

            await JoinableTaskFactory.SwitchToMainThreadAsync();
            OutputLog.Write(error);
            OutputLog.SetStatus("SQLFluff: not found. Install with 'pip install sqlfluff' or set its path in Tools > Options > SQLFluff.");
        }

        private void AddCommand(OleMenuCommandService service, int id, EventHandler handler, bool requiresSql)
        {
            var command = new OleMenuCommand(handler, new CommandID(PackageGuids.CmdSet, id));
            if (requiresSql)
            {
                command.BeforeQueryStatus += (sender, args) =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    ((OleMenuCommand)sender).Enabled = _editor.TryGetActiveSqlView(out _, out _, out _);
                };
            }

            service.AddCommand(command);
        }

        private enum EditorAction
        {
            Lint,
            Fix,
            Format,
        }

        private void RunOnActiveDocument(EditorAction action)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!_editor.TryGetActiveSqlView(out IWpfTextView view, out ITextBuffer buffer, out string path))
            {
                OutputLog.SetStatus("SQLFluff: open a SQL query window first.");
                return;
            }

            Func<Task> operation;
            switch (action)
            {
                case EditorAction.Fix:
                    operation = () => _lint.FixAsync(view, buffer, path);
                    break;
                case EditorAction.Format:
                    operation = () => _lint.FormatAsync(view, buffer, path);
                    break;
                default:
                    operation = () => _lint.LintAsync(buffer, path, userInitiated: true);
                    break;
            }

            JoinableTaskFactory.RunAsync(operation).Task.FileAndForget("sqlfluff/" + action.ToString().ToLowerInvariant());
        }

        private void ClearActiveDocument()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_editor.TryGetActiveSqlView(out _, out ITextBuffer buffer, out string path))
            {
                _lint.Clear(buffer, path);
                OutputLog.SetStatus("SQLFluff: diagnostics cleared.");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                if (_rdt != null && _rdtCookie != 0)
                {
                    _rdt.UnadviseRunningDocTableEvents(_rdtCookie);
                    _rdtCookie = 0;
                }

                _errors?.Dispose();
                _errors = null;

                if (Instance == this)
                {
                    Instance = null;
                }
            }

            base.Dispose(disposing);
        }
    }
}
