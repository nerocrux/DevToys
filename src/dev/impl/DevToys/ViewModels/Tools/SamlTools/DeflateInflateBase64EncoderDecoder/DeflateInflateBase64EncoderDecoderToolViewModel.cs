#nullable enable

using System;
using System.Collections.Generic;
using System.Composition;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using DevToys.Api.Core;
using DevToys.Api.Core.Settings;
using DevToys.Api.Tools;
using DevToys.Core;
using DevToys.Core.Threading;
using DevToys.Shared.Core.Threading;
using DevToys.Views.Tools.DeflateInflateBase64EncoderDecoder;
using Microsoft.Toolkit.Mvvm.ComponentModel;

namespace DevToys.ViewModels.Tools.DeflateInflateBase64EncoderDecoder
{
    [Export(typeof(DeflateInflateBase64EncoderDecoderToolViewModel))]
    public class DeflateInflateBase64EncoderDecoderToolViewModel : ObservableRecipient, IToolViewModel
    {
        /// <summary>
        /// Whether the tool should encode or decode Base64.
        /// </summary>
        private static readonly SettingDefinition<bool> EncodeMode
            = new(
                name: $"{nameof(DeflateInflateBase64EncoderDecoderToolViewModel)}.{nameof(EncodeMode)}",
                isRoaming: true,
                defaultValue: true);

        /// <summary>
        /// Whether the tool should encode/decode in Unicode or ASCII.
        /// </summary>
        private static readonly SettingDefinition<string> Encoder
            = new(
                name: $"{nameof(DeflateInflateBase64EncoderDecoderToolViewModel)}.{nameof(Encoder)}",
                isRoaming: true,
                defaultValue: DefaultEncoding);

        private const string DefaultEncoding = "UTF-8";

        private readonly IMarketingService _marketingService;
        private readonly ISettingsProvider _settingsProvider;
        private readonly Queue<string> _conversionQueue = new();

        private string? _inputValue;
        private string? _outputValue;
        private bool _conversionInProgress;
        private bool _setPropertyInProgress;
        private bool _toolSuccessfullyWorked;

        public Type View { get; } = typeof(DeflateInflateBase64EncoderDecoderToolPage);

        internal DeflateInflateBase64EncoderDecoderStrings Strings => LanguageManager.Instance.DeflateInflateBase64EncoderDecoder;

        /// <summary>
        /// Gets or sets the input text.
        /// </summary>
        internal string? InputValue
        {
            get => _inputValue;
            set
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                SetProperty(ref _inputValue, value);
                QueueConversionCalculation();
            }
        }

        /// <summary>
        /// Gets or sets the output text.
        /// </summary>
        internal string? OutputValue
        {
            get => _outputValue;
            private set => SetProperty(ref _outputValue, value);
        }

        /// <summary>
        /// Gets or sets the conversion mode.
        /// </summary>
        internal bool IsEncodeMode
        {
            get => _settingsProvider.GetSetting(EncodeMode);
            set
            {
                if (!_setPropertyInProgress)
                {
                    _setPropertyInProgress = true;
                    ThreadHelper.ThrowIfNotOnUIThread();
                    if (_settingsProvider.GetSetting(EncodeMode) != value)
                    {
                        _settingsProvider.SetSetting(EncodeMode, value);
                        OnPropertyChanged();
                    }
                    InputValue = OutputValue;
                    _setPropertyInProgress = false;
                }
            }
        }

        /// <summary>
        /// Gets or sets the encoding mode.
        /// </summary>
        internal string EncodingMode
        {
            get => _settingsProvider.GetSetting(Encoder);
            set
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                if (!string.Equals(_settingsProvider.GetSetting(Encoder), value, StringComparison.Ordinal))
                {
                    _settingsProvider.SetSetting(Encoder, value);
                    OnPropertyChanged();
                    QueueConversionCalculation();
                }
            }
        }

        [ImportingConstructor]
        public DeflateInflateBase64EncoderDecoderToolViewModel(ISettingsProvider settingsProvider, IMarketingService marketingService)
        {
            _settingsProvider = settingsProvider;
            _marketingService = marketingService;
        }

        private void QueueConversionCalculation()
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
                string conversionResult;
                if (IsEncodeMode)
                {
                    conversionResult = await EncodeBase64DataAsync(text).ConfigureAwait(false);
                }
                else
                {
                    conversionResult = await DecodeBase64DataAsync(text).ConfigureAwait(false);
                }

                ThreadHelper.RunOnUIThreadAsync(ThreadPriority.Low, () =>
                {
                    OutputValue = conversionResult;

                    if (!_toolSuccessfullyWorked)
                    {
                        _toolSuccessfullyWorked = true;
                        _marketingService.NotifyToolSuccessfullyWorked();
                    }
                }).ForgetSafely();
            }

            _conversionInProgress = false;
        }

        private async Task<string> EncodeBase64DataAsync(string? data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return string.Empty;
            }

            await TaskScheduler.Default;

            string? encoded;
            try
            {
                // Deflate
                using (var output = new MemoryStream())
                {
                    // Uglify XML
                    var element = XElement.Parse(data);
                    var stringBuilder = new StringBuilder();
                    var settings = new XmlWriterSettings();
                    settings.OmitXmlDeclaration = true;
                    settings.Indent = false;
                    settings.NewLineOnAttributes = false;

                    using (var xmlWriter = XmlWriter.Create(stringBuilder, settings))
                    {
                        element.Save(xmlWriter);
                    }

                    byte[] bytes = Encoding.ASCII.GetBytes(stringBuilder.ToString());

                    // Inflate
                    using (var zip = new DeflateStream(output, CompressionMode.Compress, true))
                    {
                        zip.Write(bytes, 0, bytes.Length);
                    }

                    output.Position = 0;
                    byte[] compressed = new byte[output.Length];
                    output.Read(compressed, 0, compressed.Length);

                    // Base 64 Encode + URL Encode
                    Encoding encoder = GetEncoder();
                    encoded = HttpUtility.UrlEncode(Convert.ToBase64String(compressed));
                }
            }
            catch (XmlException ex)
            {
                Logger.LogFault("Deflate + Base64 Encode XML error", ex, $"Encoding mode: {EncodingMode}");
                return ex.Message;
            }
            catch (Exception ex)
            {
                Logger.LogFault("Deflate + Base64 Encode error", ex, $"Encoding mode: {EncodingMode}");
                return ex.Message;
            }

            return encoded;
        }

        private async Task<string> DecodeBase64DataAsync(string? data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return string.Empty;
            }

            await TaskScheduler.Default;
            string? decoded = string.Empty;

            try
            {
                // URL Decode + Base 64 Decode
                var bytes = Convert.FromBase64String(HttpUtility.UrlDecode(data));
                using (var output = new MemoryStream())
                {
                    using (var input = new MemoryStream(bytes))
                    {
                        // Inflate
                        using (var unzip = new DeflateStream(input, CompressionMode.Decompress))
                        {
                            unzip.CopyTo(output, bytes.Length);
                            unzip.Close();
                        }
                        var decodedUgly = Encoding.UTF8.GetString(output.ToArray());

                        // XML Prettify
                        var element = XElement.Parse(decodedUgly);
                        var stringBuilder = new StringBuilder();
                        var settings = new XmlWriterSettings();
                        settings.OmitXmlDeclaration = true;
                        settings.Indent = true;
                        settings.NewLineOnAttributes = true;

                        using (var xmlWriter = XmlWriter.Create(stringBuilder, settings))
                        {
                            element.Save(xmlWriter);
                        }
                        decoded = stringBuilder.ToString();
                    }
                }
            }
            catch (XmlException ex)
            {
                Logger.LogFault("Base64 Decode + Inflate XML error", ex, $"Encoding mode: {EncodingMode}");
                return ex.Message;
            }
            catch (FormatException ex)
            {
                Logger.LogFault("Base64 Decode + Inflate Format error", ex, $"Encoding mode: {EncodingMode}");
                return ex.Message;
            }
            catch (Exception ex)
            {
                Logger.LogFault("Base 64 Decode + Inflate", ex, $"Encoding mode: {EncodingMode}");
                return ex.Message;
            }

            return decoded;
        }

        private Encoding GetEncoder()
        {
            if (string.Equals(EncodingMode, DefaultEncoding, StringComparison.Ordinal))
            {
                return Encoding.UTF8;
            }
            return Encoding.ASCII;
        }
    }
}
