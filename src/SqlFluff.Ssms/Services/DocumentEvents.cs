using System;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;

namespace SqlFluff.Ssms.Services
{
    internal sealed class DocumentEvents : IVsRunningDocTableEvents3
    {
        private readonly SqlFluffPackage _package;
        private readonly IVsRunningDocumentTable _rdt;
        private readonly EditorServices _editor;
        private readonly LintService _lint;
        private readonly ConditionalWeakTable<ITextBuffer, object> _tracked = new ConditionalWeakTable<ITextBuffer, object>();

        public DocumentEvents(SqlFluffPackage package, IVsRunningDocumentTable rdt, EditorServices editor, LintService lint)
        {
            _package = package;
            _rdt = rdt;
            _editor = editor;
            _lint = lint;
        }

        public void AttachToOpenDocuments()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (ErrorHandler.Failed(_rdt.GetRunningDocumentsEnum(out IEnumRunningDocuments enumerator)))
            {
                return;
            }

            var cookies = new uint[1];
            while (enumerator.Next(1, cookies, out uint fetched) == VSConstants.S_OK && fetched == 1)
            {
                Attach(cookies[0]);
            }
        }

        public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
        {
            if (fFirstShow != 0)
            {
                Attach(docCookie);
            }

            return VSConstants.S_OK;
        }

        public int OnAfterSave(uint docCookie)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_package.GetSettings().LintOnSave &&
                _editor.TryGetBufferFromDocCookie(_rdt, docCookie, out ITextBuffer buffer, out string path))
            {
                RunLint(buffer, path);
            }

            return VSConstants.S_OK;
        }

        public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (dwReadLocksRemaining == 0 && dwEditLocksRemaining == 0 &&
                _editor.TryGetBufferFromDocCookie(_rdt, docCookie, out _, out string path))
            {
                _lint.DocumentClosed(path);
            }

            return VSConstants.S_OK;
        }

        private void Attach(uint docCookie)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!_editor.TryGetBufferFromDocCookie(_rdt, docCookie, out ITextBuffer buffer, out string path))
            {
                return;
            }

            bool firstTime = false;
            _tracked.GetValue(buffer, b =>
            {
                firstTime = true;
                b.PostChanged += OnBufferChanged;
                return new object();
            });

            if (firstTime && _package.GetSettings().LintOnOpen)
            {
                RunLint(buffer, path);
            }
        }

        private void OnBufferChanged(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var buffer = (ITextBuffer)sender;
            SqlFluff.Ssms.Core.SqlFluffSettings settings = _package.GetSettings();
            if (settings.LintOnType)
            {
                _lint.Schedule(buffer, _editor.GetPath(buffer), settings.TypeDelayMs);
            }
        }

        private void RunLint(ITextBuffer buffer, string path)
        {
            ThreadHelper.JoinableTaskFactory
                .RunAsync(() => _lint.LintAsync(buffer, path, userInitiated: false))
                .Task.FileAndForget("sqlfluff/lint");
        }

        public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) => VSConstants.S_OK;
        public int OnAfterAttributeChange(uint docCookie, uint grfAttribs) => VSConstants.S_OK;
        public int OnAfterAttributeChangeEx(uint docCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld, string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew) => VSConstants.S_OK;
        public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame) => VSConstants.S_OK;
        public int OnBeforeSave(uint docCookie) => VSConstants.S_OK;
    }
}
