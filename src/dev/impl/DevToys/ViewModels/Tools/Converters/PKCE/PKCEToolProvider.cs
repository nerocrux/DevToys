#nullable enable

using System.Composition;
using DevToys.Shared.Api.Core;
using DevToys.Api.Tools;
using DevToys.Core.Threading;
using Windows.UI.Xaml.Controls;

namespace DevToys.ViewModels.Tools.PKCE
{
    [Export(typeof(IToolProvider))]
    [Name("PKCE")]
    [Parent(ConvertersGroupToolProvider.InternalName)]
    [ProtocolName("pkce")]
    [Order(0)]
    [NotScrollable]
    internal sealed class PKCEToolProvider : ToolProviderBase, IToolProvider
    {
        private readonly IMefProvider _mefProvider;

        public string MenuDisplayName => LanguageManager.Instance.PKCE.MenuDisplayName;

        public string? SearchDisplayName => LanguageManager.Instance.PKCE.SearchDisplayName;

        public string? Description => LanguageManager.Instance.PKCE.Description;

        public string AccessibleName => LanguageManager.Instance.PKCE.AccessibleName;

        public string? SearchKeywords => LanguageManager.Instance.PKCE.SearchKeywords;

        public TaskCompletionNotifier<IconElement> IconSource => CreateSvgIcon("PKCE.svg");

        [ImportingConstructor]
        public PKCEToolProvider(IMefProvider mefProvider)
        {
            _mefProvider = mefProvider;
        }

        public bool CanBeTreatedByTool(string data)
        {
            return !string.IsNullOrWhiteSpace(data);
        }

        public IToolViewModel CreateTool()
        {
            return _mefProvider.Import<PKCEToolViewModel>();
        }
    }
}
