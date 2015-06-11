﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using BundlerMinifier;
using Microsoft.VisualStudio.Shell;

namespace BundlerMinifierVsix.Commands
{
    internal sealed class CreateBundle
    {
        private readonly Package _package;

        private CreateBundle(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            _package = package;

            OleMenuCommandService commandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var menuCommandID = new CommandID(GuidList.guidBundlerCmdSet, PackageCommands.CreateBundleId);
                var menuItem = new OleMenuCommand(AddBundle, menuCommandID);
                menuItem.BeforeQueryStatus += BeforeQueryStatus;
                commandService.AddCommand(menuItem);
            }
        }

        private void BeforeQueryStatus(object sender, EventArgs e)
        {
            var button = (OleMenuCommand)sender;
            var files = ProjectHelpers.GetSelectedItemPaths();

            button.Visible = BundleFileProcessor.IsSupported(files);
        }

        public static CreateBundle Instance
        {
            get;
            private set;
        }

        private IServiceProvider ServiceProvider
        {
            get
            {
                return _package;
            }
        }

        public static void Initialize(Package package)
        {
            Instance = new CreateBundle(package);
        }

        private void AddBundle(object sender, EventArgs e)
        {
            var item = ProjectHelpers.GetSelectedItems().ElementAt(0);

            if (item.ContainingProject == null)
                return;

            string folder = ProjectHelpers.GetRootFolder(item.ContainingProject);
            string configFile = Path.Combine(folder, FileHelpers.FILENAME);
            IEnumerable<string> files = ProjectHelpers.GetSelectedItemPaths().Select(f => MakeRelative(configFile, f));
            string outputFile = GetOutputFileName(folder, Path.GetExtension(files.ElementAt(0)));

            if (string.IsNullOrEmpty(outputFile))
                return;

            string relativeOutputFile = MakeRelative(configFile, outputFile);
            Bundle bundle = CreateBundleFile(files, relativeOutputFile);

            Bundler bundler = new Bundler();
            bundler.AddBundle(configFile, bundle);

            ProjectHelpers.AddFileToProject(item.ContainingProject, configFile, "None");
            BundlerMinifierPackage._dte.ItemOperations.OpenFile(configFile);
            BundleService.Process(configFile);
        }

        private static Bundle CreateBundleFile(IEnumerable<string> files, string outputFile)
        {
            return new Bundle
            {
                IncludeInProject = true,
                OutputFileName = outputFile,
                InputFiles = new List<string>(files)
            };
        }

        private static string MakeRelative(string baseFile, string file)
        {
            Uri baseUri = new Uri(baseFile, UriKind.RelativeOrAbsolute);
            Uri fileUri = new Uri(file, UriKind.RelativeOrAbsolute);

            return baseUri.MakeRelativeUri(fileUri).ToString();
        }

        private static string GetOutputFileName(string folder, string extension)
        {
            string ext = extension.TrimStart('.');

            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.InitialDirectory = folder;
                dialog.DefaultExt = ext;
                dialog.FileName = "bundle";
                dialog.Filter = ext.ToUpperInvariant() + " File|*." + ext;

                DialogResult result = dialog.ShowDialog();

                if (result == DialogResult.OK)
                    return dialog.FileName;
            }

            return null;
        }
    }
}
