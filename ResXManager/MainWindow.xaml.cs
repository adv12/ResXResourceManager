﻿namespace tomenglertde.ResXManager
{
    using System;
    using System.ComponentModel;
    using System.ComponentModel.Composition;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Controls.Primitives;
    using System.Windows.Documents;

    using JetBrains.Annotations;

    using tomenglertde.ResXManager.Infrastructure;
    using tomenglertde.ResXManager.Model;
    using tomenglertde.ResXManager.Properties;

    using TomsToolbox.Composition;
    using TomsToolbox.Wpf;
    using TomsToolbox.Wpf.Composition;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    [Export]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public partial class MainWindow
    {
        [NotNull]
        private readonly ITracer _tracer;
        private Size _lastKnownSize;
        private Vector _lastKnownLocation;

        [ImportingConstructor]
        public MainWindow([NotNull] IExportProvider exportProvider, [NotNull] ITracer tracer)
        {
            _tracer = tracer;

            try
            {
                this.SetExportProvider(exportProvider);

                InitializeComponent();

                AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(Navigate_Click));
            }
            catch (Exception ex)
            {
                _tracer.TraceError(ex.ToString());
            }
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            var size = Settings.StartupSize;

            Width = Math.Max(100, size.Width);
            Height = Math.Max(100, size.Height);

            var location = Settings.StartupLocation;

            if ((location.X > SystemParameters.VirtualScreenWidth - 100)
                || (location.Y > SystemParameters.VirtualScreenHeight - 100)
                || (location.X < SystemParameters.VirtualScreenLeft)
                || (location.Y < SystemParameters.VirtualScreenTop))
                return;

            Left = Math.Max(0, location.X);
            Top = Math.Max(0, location.Y);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            var resourceManager = this.GetExportProvider().GetExportedValue<ResourceManager>();

            if (!resourceManager.HasChanges)
                return;

            switch (MessageBox.Show(Properties.Resources.QuerySaveChanges, View.Properties.Resources.Title, MessageBoxButton.YesNoCancel))
            {
                case MessageBoxResult.Cancel:
                    e.Cancel = true;
                    break;

                case MessageBoxResult.No:
                    break;

                case MessageBoxResult.Yes:
                    try
                    {
                        resourceManager.Save();
                    }
                    catch (Exception ex)
                    {
                        _tracer.TraceError(ex.ToString());
                        MessageBox.Show(ex.Message);
                        e.Cancel = true;
                    }
                    break;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            Settings.StartupLocation = _lastKnownLocation;
            Settings.StartupSize = _lastKnownSize;
        }

        [NotNull]
        private static Settings Settings => Settings.Default;

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            if (WindowState == WindowState.Normal)
                _lastKnownSize = sizeInfo.NewSize;
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);

            if (WindowState == WindowState.Normal)
                _lastKnownLocation = new Vector(Left, Top);
        }

        private static void Navigate_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            string url;

            if (e.OriginalSource is FrameworkElement source)
            {
                var button = source.TryFindAncestorOrSelf<ButtonBase>();
                if (button == null)
                    return;

                url = source.Tag as string;
                if (string.IsNullOrEmpty(url) || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    return;
            }
            else
            {
                var link = e.OriginalSource as Hyperlink;

                var navigateUri = link?.NavigateUri;
                if (navigateUri == null)
                    return;

                url = navigateUri.ToString();
            }

            Process.Start(url);
        }
    }
}