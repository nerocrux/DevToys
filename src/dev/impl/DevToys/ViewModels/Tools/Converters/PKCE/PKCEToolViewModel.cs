#nullable enable

using System;
using System.Collections.Generic;
using System.Composition;
using System.Text;
using System.Threading.Tasks;
using DevToys.Api.Core;
using DevToys.Api.Core.Settings;
using DevToys.Api.Tools;
using DevToys.Core;
using DevToys.Core.Threading;
using DevToys.Shared.Core.Threading;
using DevToys.Views.Tools.PKCE;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using YamlDotNet.Core;
using System.Security.Cryptography;

namespace DevToys.ViewModels.Tools.PKCE
{
    [Export(typeof(PKCEToolViewModel))]
    public sealed class PKCEToolViewModel : ObservableRecipient, IToolViewModel
    {
        /// <summary>
        /// Whether the tool should convert Challenge to Verifier or Verifier to Challenge.
        /// </summary>
        private static readonly SettingDefinition<string> Conversion
            = new(
                name: $"{nameof(PKCEToolViewModel)}.{nameof(Conversion)}",
                isRoaming: true,
                defaultValue: ChallengeToVerifier);


        internal const string ChallengeToVerifier = nameof(ChallengeToVerifier);
        internal const string VerifierToChallenge = nameof(VerifierToChallenge);

        private readonly IMarketingService _marketingService;
        private readonly Queue<string> _conversionQueue = new();

        private bool _toolSuccessfullyWorked;
        private bool _conversionInProgress;
        private bool _setPropertyInProgress;
        private string? _inputValue;
        private string? _inputValueLanguage;
        private string? _outputValue;
        private string? _outputValueLanguage;

        public Type View { get; } = typeof(PKCEToolPage);

        internal PKCEStrings Strings => LanguageManager.Instance.PKCE;

        /// <summary>
        /// Gets or sets the desired conversion mode.
        /// </summary>
        internal string ConversionMode
        {
            get => SettingsProvider.GetSetting(Conversion);
            set
            {
                if (!_setPropertyInProgress)
                {
                    _setPropertyInProgress = true;
                    ThreadHelper.ThrowIfNotOnUIThread();
                    if (!string.Equals(SettingsProvider.GetSetting(Conversion), value, StringComparison.Ordinal))
                    {
                        SettingsProvider.SetSetting(Conversion, value);
                        OnPropertyChanged();
                    }

                    _setPropertyInProgress = false;
                }
            }
        }

        /// <summary>
        /// Gets or sets the input text.
        /// </summary>
        internal string? InputValue
        {
            get => _inputValue;
            set
            {
                SetProperty(ref _inputValue, value);
                QueueConversion();
            }
        }

        /// <summary>
        /// Gets or sets the input code editor's language.
        /// </summary>
        internal string? InputValueLanguage
        {
            get => _inputValueLanguage;
            set => SetProperty(ref _inputValueLanguage, value);
        }

        /// <summary>
        /// Gets or sets the output text.
        /// </summary>
        internal string? OutputValue
        {
            get => _outputValue;
            set => SetProperty(ref _outputValue, value);
        }

        /// <summary>
        /// Gets or sets the output code editor's language.
        /// </summary>
        internal string? OutputValueLanguage
        {
            get => _outputValueLanguage;
            set => SetProperty(ref _outputValueLanguage, value);
        }

        internal ISettingsProvider SettingsProvider { get; }

        [ImportingConstructor]
        public PKCEToolViewModel(ISettingsProvider settingsProvider, IMarketingService marketingService)
        {
            SettingsProvider = settingsProvider;
            _marketingService = marketingService;
            InputValueLanguage = "pkce_verifier";
            OutputValueLanguage = "pkce_challenge";
        }

        private void QueueConversion()
        {
            _conversionQueue.Enqueue(InputValue ?? string.Empty);
            TreatQueueAsync().Forget();
        }

        private async Task TreatQueueAsync()
        {
            if (_conversionInProgress)
            {
                return;
            }

            _conversionInProgress = true;

            await TaskScheduler.Default;

            while (_conversionQueue.TryDequeue(out string? text))
            {
                bool success;
                string result;

                success = ConvertVerifierToChallenge(text, out result);

                ThreadHelper.RunOnUIThreadAsync(ThreadPriority.Low, () =>
                {
                    OutputValue = result;

                    if (success && !_toolSuccessfullyWorked)
                    {
                        _toolSuccessfullyWorked = true;
                        _marketingService.NotifyToolSuccessfullyWorked();
                    }
                }).ForgetSafely();
            }

            _conversionInProgress = false;
        }

        private bool ConvertVerifierToChallenge(string input, out string output)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                output = string.Empty;
                return false;
            }

            try
            {
                string codeChallenge;
                using (var sha256 = SHA256.Create())
                {
                    byte[] challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                    codeChallenge = Convert.ToBase64String(challengeBytes)
                        .TrimEnd('=')
                        .Replace('+', '-')
                        .Replace('/', '_');
                }
                output = codeChallenge;
                return true;
            }
            catch (SemanticErrorException ex)
            {
                output = ex.Message;
            }
            catch (Exception ex)
            {
                Logger.LogFault("Verifier to Challenge Converter", ex);
                output = string.Empty;
            }

            return false;
        }
    }
}
