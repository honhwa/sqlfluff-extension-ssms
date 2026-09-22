using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using SqlFluff.Ssms.Core;
using SqlFluff.Ssms.Editor;

namespace SqlFluff.Ssms.Services
{
    internal sealed class ErrorListService : IDisposable
    {
        private readonly ErrorListProvider _provider;
        private readonly Dictionary<string, List<ErrorTask>> _tasksByPath =
            new Dictionary<string, List<ErrorTask>>(StringComparer.OrdinalIgnoreCase);

        public ErrorListService(IServiceProvider serviceProvider)
        {
            _provider = new ErrorListProvider(serviceProvider)
            {
                ProviderName = "SQLFluff",
                ProviderGuid = PackageGuids.ErrorListProvider,
            };
        }

        public void Publish(string path, ViolationSet set, Action<ViolationEntry> navigate)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string key = path ?? string.Empty;

            _provider.SuspendRefresh();
            try
            {
                RemoveTasks(key);

                var created = new List<ErrorTask>(set.Entries.Count);
                foreach (ViolationEntry entry in set.Entries)
                {
                    ITextSnapshotLine line = set.Snapshot.GetLineFromPosition(Math.Min(entry.Span.Start, set.Snapshot.Length));
                    var task = new ErrorTask
                    {
                        Text = entry.Violation.Message,
                        Document = path,
                        Line = line.LineNumber,
                        Column = Math.Max(0, entry.Span.Start - line.Start.Position),
                        Category = TaskCategory.Misc,
                        ErrorCategory = CategoryFor(entry.Violation, set.Severity),
                        Priority = TaskPriority.Normal,
                        CanDelete = false,
                    };

                    ViolationEntry captured = entry;
                    task.Navigate += (sender, args) => navigate(captured);

                    _provider.Tasks.Add(task);
                    created.Add(task);
                }

                _tasksByPath[key] = created;
            }
            finally
            {
                _provider.ResumeRefresh();
            }
        }

        public void Clear(string path)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _provider.SuspendRefresh();
            try
            {
                RemoveTasks(path ?? string.Empty);
            }
            finally
            {
                _provider.ResumeRefresh();
            }
        }

        public void Show()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _provider.Show();
        }

        public void Dispose()
        {
            _provider.Tasks.Clear();
            _provider.Dispose();
        }

        private void RemoveTasks(string key)
        {
            if (_tasksByPath.TryGetValue(key, out List<ErrorTask> existing))
            {
                foreach (ErrorTask task in existing)
                {
                    _provider.Tasks.Remove(task);
                }

                _tasksByPath.Remove(key);
            }
        }

        private static TaskErrorCategory CategoryFor(LintViolation violation, DiagnosticSeverity severity)
        {
            if (violation.IsParseError)
            {
                return TaskErrorCategory.Error;
            }

            switch (severity)
            {
                case DiagnosticSeverity.Error:
                    return TaskErrorCategory.Error;
                case DiagnosticSeverity.Message:
                    return TaskErrorCategory.Message;
                default:
                    return TaskErrorCategory.Warning;
            }
        }
    }
}
