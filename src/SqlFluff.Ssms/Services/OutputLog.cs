using System;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace SqlFluff.Ssms.Services
{
    internal static class OutputLog
    {
        private static readonly Guid PaneGuid = new Guid("5b9d2e7f-4c18-4a63-8e0b-1f7a3d6c9e42");
        private static IVsOutputWindowPane _pane;

        public static void Write(string message)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                IVsOutputWindowPane pane = GetPane();
                pane?.OutputStringThreadSafe("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine);
            });
        }

        public static void SetStatus(string text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (Package.GetGlobalService(typeof(SVsStatusbar)) is IVsStatusbar statusBar)
            {
                statusBar.SetText(text);
            }
        }

        private static IVsOutputWindowPane GetPane()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_pane != null)
            {
                return _pane;
            }

            if (Package.GetGlobalService(typeof(SVsOutputWindow)) is IVsOutputWindow window)
            {
                Guid guid = PaneGuid;
                window.CreatePane(ref guid, "SQLFluff", 1, 1);
                window.GetPane(ref guid, out _pane);
            }

            return _pane;
        }
    }
}
