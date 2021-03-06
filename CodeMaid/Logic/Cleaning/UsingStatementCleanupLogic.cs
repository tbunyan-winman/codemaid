using EnvDTE;
using SteveCadwallader.CodeMaid.Helpers;
using SteveCadwallader.CodeMaid.Properties;
using System;
using System.Linq;

namespace SteveCadwallader.CodeMaid.Logic.Cleaning
{
    /// <summary>
    /// A class for encapsulating using statement cleanup logic.
    /// </summary>
    internal class UsingStatementCleanupLogic
    {
        #region Fields

        private readonly CodeMaidPackage _package;

        private readonly CachedSettingSet<string> _usingStatementsToReinsertWhenRemoved =
            new CachedSettingSet<string>(() => Settings.Default.Cleaning_UsingStatementsToReinsertWhenRemovedExpression,
                                         expression =>
                                         expression.Split(new[] { "||" }, StringSplitOptions.RemoveEmptyEntries)
                                                   .Select(x => x.Trim())
                                                   .Where(y => !string.IsNullOrEmpty(y))
                                                   .ToList());

        #endregion Fields

        #region Constructors

        /// <summary>
        /// The singleton instance of the <see cref="UsingStatementCleanupLogic" /> class.
        /// </summary>
        private static UsingStatementCleanupLogic _instance;

        /// <summary>
        /// Gets an instance of the <see cref="UsingStatementCleanupLogic" /> class.
        /// </summary>
        /// <param name="package">The hosting package.</param>
        /// <returns>An instance of the <see cref="UsingStatementCleanupLogic" /> class.</returns>
        internal static UsingStatementCleanupLogic GetInstance(CodeMaidPackage package)
        {
            return _instance ?? (_instance = new UsingStatementCleanupLogic(package));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UsingStatementCleanupLogic" /> class.
        /// </summary>
        /// <param name="package">The hosting package.</param>
        private UsingStatementCleanupLogic(CodeMaidPackage package)
        {
            _package = package;
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Run the visual studio built-in remove unused using statements command.
        /// </summary>
        /// <param name="textDocument">The text document to update.</param>
        public void RemoveUnusedUsingStatements(TextDocument textDocument)
        {
            if (!Settings.Default.Cleaning_RunVisualStudioRemoveUnusedUsingStatements) return;
            if (_package.IsAutoSaveContext && Settings.Default.Cleaning_SkipRemoveUnusedUsingStatementsDuringAutoCleanupOnSave) return;

            // Capture all existing using statements that should be re-inserted if removed.
            const string patternFormat = @"^[ \t]*{0}[ \t]*\r?\n";

            var points = (from usingStatement in _usingStatementsToReinsertWhenRemoved.Value
                          from editPoint in TextDocumentHelper.FindMatches(textDocument, string.Format(patternFormat, usingStatement))
                          select new { editPoint, text = editPoint.GetLine() }).Reverse().ToList();

            // Shift every captured point one character to the right so they will auto-advance
            // during new insertions at the start of the line.
            foreach (var point in points)
            {
                point.editPoint.CharRight();
            }

            _package.IDE.ExecuteCommand("Edit.RemoveUnusedUsings", string.Empty);

            // Check each using statement point and re-insert it if removed.
            foreach (var point in points)
            {
                string text = point.editPoint.GetLine();
                if (text != point.text)
                {
                    point.editPoint.StartOfLine();
                    point.editPoint.Insert(point.text);
                    point.editPoint.Insert(Environment.NewLine);
                }
            }
        }

        /// <summary>
        /// Run the visual studio built-in sort using statements command.
        /// </summary>
        public void SortUsingStatements()
        {
            if (!Settings.Default.Cleaning_RunVisualStudioSortUsingStatements) return;
            if (_package.IsAutoSaveContext && Settings.Default.Cleaning_SkipSortUsingStatementsDuringAutoCleanupOnSave) return;

            _package.IDE.ExecuteCommand("Edit.SortUsings", string.Empty);
        }

        #endregion Methods
    }
}