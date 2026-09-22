using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.Text;
using SqlFluff.Ssms.Core;

namespace SqlFluff.Ssms.Editor
{
    internal sealed class ViolationEntry
    {
        public ViolationEntry(LintViolation violation, Span span)
        {
            Violation = violation;
            Span = span;
        }

        public LintViolation Violation { get; }
        public Span Span { get; }
    }

    internal sealed class ViolationSet
    {
        public ViolationSet(ITextSnapshot snapshot, IReadOnlyList<ViolationEntry> entries, DiagnosticSeverity severity)
        {
            Snapshot = snapshot;
            Entries = entries;
            Severity = severity;
        }

        public ITextSnapshot Snapshot { get; }
        public IReadOnlyList<ViolationEntry> Entries { get; }
        public DiagnosticSeverity Severity { get; }
    }

    // Keyed by the document buffer; the squiggle tagger and the Error List both read from here.
    internal static class ViolationStore
    {
        private sealed class Holder
        {
            public ViolationSet Value;
            public Action Changed;
        }

        private static readonly ConditionalWeakTable<ITextBuffer, Holder> Table = new ConditionalWeakTable<ITextBuffer, Holder>();

        // Handlers live in the per-buffer holder, so nothing static keeps a buffer alive.
        public static void Subscribe(ITextBuffer buffer, Action handler)
        {
            Table.GetOrCreateValue(buffer).Changed += handler;
        }

        public static ViolationSet Get(ITextBuffer buffer)
        {
            return Table.TryGetValue(buffer, out Holder holder) ? holder.Value : null;
        }

        public static void Set(ITextBuffer buffer, ViolationSet set)
        {
            Holder holder = Table.GetOrCreateValue(buffer);
            holder.Value = set;
            holder.Changed?.Invoke();
        }

        public static void Clear(ITextBuffer buffer)
        {
            if (Table.TryGetValue(buffer, out Holder holder) && holder.Value != null)
            {
                holder.Value = null;
                holder.Changed?.Invoke();
            }
        }

        public static Span ToSpan(ITextSnapshot snapshot, LintViolation v)
        {
            int length = snapshot.Length;
            if (length == 0)
            {
                return new Span(0, 0);
            }

            int start = ToPosition(snapshot, v.StartLine, v.StartColumn);
            int end = v.EndLine > 0
                ? ToPosition(snapshot, v.EndLine, v.EndColumn)
                : ExtendToWordEnd(snapshot, start);

            if (start >= length)
            {
                // Violations reported at end-of-file: highlight the last character instead of nothing.
                start = length - 1;
                end = length;
            }

            if (end <= start)
            {
                end = Math.Min(length, start + 1);
            }

            return Span.FromBounds(start, end);
        }

        private static int ToPosition(ITextSnapshot snapshot, int line, int column)
        {
            int lineIndex = Math.Min(Math.Max(line - 1, 0), snapshot.LineCount - 1);
            ITextSnapshotLine textLine = snapshot.GetLineFromLineNumber(lineIndex);
            int offset = Math.Min(Math.Max(column - 1, 0), textLine.Length);
            return textLine.Start.Position + offset;
        }

        private static int ExtendToWordEnd(ITextSnapshot snapshot, int start)
        {
            int position = start;
            while (position < snapshot.Length && IsWordChar(snapshot[position]))
            {
                position++;
            }

            return position;
        }

        private static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '@' || c == '#';
        }
    }
}
