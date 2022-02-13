#nullable enable

using System.Composition;
using DevToys.Api.Tools;
using DevToys.Core.Threading;
using DevToys.Shared.Api.Core;
using Windows.UI.Xaml.Controls;

namespace DevToys.ViewModels.Tools
{
    [Export(typeof(IToolProvider))]
    [Name(InternalName)]
    [ProtocolName("samltools")]
    [Order(0)]
    [NotSearchable]
    [NoCompactOverlaySupport]
    internal sealed class SamlToolsGroupToolProvider : GroupToolProviderBase
    {
        internal const string InternalName = "SamlTools";

        public override string MenuDisplayName => LanguageManager.Instance.ToolGroups.SamlToolsDisplayName;

        public override string AccessibleName => LanguageManager.Instance.ToolGroups.SamlToolsAccessibleName;

        public override TaskCompletionNotifier<IconElement> IconSource => CreateSvgIcon("Saml.svg");

        [ImportingConstructor]
        public SamlToolsGroupToolProvider(IMefProvider mefProvider)
            : base(mefProvider)
        {
        }
    }
}
