using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
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

        internal SqlFluffSettings GetSettings()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return ((SqlFluffOptionsPage)GetDialogPage(typeof(SqlFluffOptionsPage))).ToSettings();
        }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

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
                AddCommand(commands, PackageIds.CmdLint, (s, e) => RunOnActiveDocument(false), requiresSql: true);
                AddCommand(commands, PackageIds.CmdFix, (s, e) => RunOnActiveDocument(true), requiresSql: true);
                AddCommand(commands, PackageIds.CmdClear, (s, e) => ClearActiveDocument(), requiresSql: true);
                AddCommand(commands, PackageIds.CmdOptions, (s, e) => ShowOptionPage(typeof(SqlFluffOptionsPage)), requiresSql: false);
            }

            _rdt = (IVsRunningDocumentTable)await GetServiceAsync(typeof(SVsRunningDocumentTable));
            _documentEvents = new DocumentEvents(this, _rdt, _editor, _lint);
            _rdt.AdviseRunningDocTableEvents(_documentEvents, out _rdtCookie);
            _documentEvents.AttachToOpenDocuments();
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

        private void RunOnActiveDocument(bool fix)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!_editor.TryGetActiveSqlView(out IWpfTextView view, out ITextBuffer buffer, out string path))
            {
                OutputLog.SetStatus("SQLFluff: open a SQL query window first.");
                return;
            }

            JoinableTaskFactory
                .RunAsync(() => fix
                    ? _lint.FixAsync(view, buffer, path)
                    : _lint.LintAsync(buffer, path, userInitiated: true))
                .Task.FileAndForget(fix ? "sqlfluff/fix" : "sqlfluff/lint-command");
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
            }

            base.Dispose(disposing);
        }
    }
}
