#nullable enable

using DevToys.Api.Core.Navigation;
using DevToys.Helpers;
using DevToys.Shared.Core;
using DevToys.ViewModels.Tools.PKCE;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace DevToys.Views.Tools.PKCE
{
    public sealed partial class PKCEToolPage : Page
    {
        public static readonly DependencyProperty ViewModelProperty
            = DependencyProperty.Register(
                nameof(ViewModel),
                typeof(PKCEToolViewModel),
                typeof(PKCEToolPage),
                new PropertyMetadata(null));

        /// <summary>
        /// Gets the page's view model.
        /// </summary>
        public PKCEToolViewModel ViewModel
        {
            get => (PKCEToolViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public PKCEToolPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var parameters = (NavigationParameter)e.Parameter;

            if (ViewModel is null)
            {
                // Set the view model
                Assumes.NotNull(parameters.ViewModel, nameof(parameters.ViewModel));
                ViewModel = (PKCEToolViewModel)parameters.ViewModel!;
                DataContext = ViewModel;
            }

            base.OnNavigatedTo(e);
        }
    }
}
