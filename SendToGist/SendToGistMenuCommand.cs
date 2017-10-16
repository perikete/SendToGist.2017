//------------------------------------------------------------------------------
// <copyright file="SendToGistMenuCommand.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using SendToGist.Publisher;
using static System.String;

namespace SendToGist
{
    /// <summary>
    ///     Command handler
    internal sealed class SendToGistMenuCommand
    {
        /// <summary>
        ///     Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        ///     Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("89b87bfc-e2cf-4c3f-a94d-28c23a1ac73a");

        /// <summary>
        ///     VS Package that provides this command, not null.
        /// </summary>
        private readonly Package package;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SendToGistMenuCommand" /> class.
        ///     Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private SendToGistMenuCommand(Package package)
        {
            if (package == null)
                throw new ArgumentNullException("package");

            this.package = package;

            var commandService = ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var menuCommandID = new CommandID(CommandSet, CommandId);
                var menuItem = new OleMenuCommand(MenuItemCallback, OnBeforeQueryStatus, OnBeforeQueryStatus,
                    menuCommandID);
                commandService.AddCommand(menuItem);
            }
        }

        /// <summary>
        ///     Gets the instance of the command.
        /// </summary>
        public static SendToGistMenuCommand Instance { get; private set; }

        /// <summary>
        ///     Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider => package;

        // enable if there is a selection in the current view.
        private void OnBeforeQueryStatus(object sender, EventArgs e)
        {
            var cmd = sender as OleMenuCommand;
            if (null != cmd)
            {
                var visible = false;

                var view = GetActiveTextView();

                if (null != view && !view.Selection.IsEmpty)
                    visible = view.Selection.SelectedSpans.Count > 0;
                cmd.Enabled = visible;
            }
        }

        // get the active WpfTextView, if there is one.
        private IWpfTextView GetActiveTextView()
        {
            IWpfTextView view = null;
            IVsTextView vTextView;

            var txtMgr =
                (IVsTextManager) ServiceProvider.GetService(typeof(SVsTextManager));

            const int mustHaveFocus = 1;
            txtMgr.GetActiveView(mustHaveFocus, null, out vTextView);

            var userData = vTextView as IVsUserData;
            if (null != userData)
            {
                object holder;
                var guidViewHost = DefGuidList.guidIWpfTextViewHost;
                userData.GetData(ref guidViewHost, out holder);
                var viewHost = (IWpfTextViewHost) holder;
                view = viewHost.TextView;
            }

            return view;
        }

        public static string GetCurrentFilename(IWpfTextView wpfTextView)
        {
            ITextDocument document;
            if (wpfTextView == null ||
                !wpfTextView.TextDataModel.DocumentBuffer.Properties.TryGetProperty(typeof(ITextDocument), out document)
            )
                return Empty;

            // If we have no document, just ignore it.
            if (document == null || document.TextBuffer == null)
                return Empty;

            return Path.GetFileName(document.FilePath);
        }

        public void SetStatusBarText(string text)
        {
            var vsStatusBar = (IVsStatusbar) ServiceProvider.GetService(typeof(SVsStatusbar));
            vsStatusBar.SetText(text);
        }

        // called when the menu item is executed.
        private void MenuItemCallback(object sender, EventArgs e)
        {
            var view = GetActiveTextView();

            if (null != view && !view.Selection.IsEmpty)
                if (view.Selection.SelectedSpans.Count > 0)
                {
                    var selectedText = view.Selection.SelectedSpans[0].GetText();

                    if (!IsNullOrWhiteSpace(selectedText))
                    {
                        var currentFilename = GetCurrentFilename(view);
                        var publisher = new GistPublisher();
                        var result =
                            publisher.Publish(new GistPublishRequest("Published", selectedText, currentFilename));

                        // bring up draft entry in browser.
                        Process.Start(result.Url);
                        Clipboard.SetText(result.Url);
                        SetStatusBarText("Copied Gist's URL to clipboard.");
                    }
                }
        }

        /// <summary>
        ///     Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(Package package)
        {
            Instance = new SendToGistMenuCommand(package);
        }
    }
}