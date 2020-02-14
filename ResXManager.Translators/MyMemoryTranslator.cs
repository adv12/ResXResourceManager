﻿namespace tomenglertde.ResXManager.Translators
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Web;
    using System.Windows.Controls;

    using JetBrains.Annotations;

    using tomenglertde.ResXManager.Infrastructure;

    using TomsToolbox.Wpf;
    using TomsToolbox.Wpf.Composition.Mef;

    [DataTemplate(typeof(MyMemoryTranslator))]
    public class MyMemoryTranslatorConfiguration : Decorator
    {
    }

    [Export(typeof(ITranslator))]
    public class MyMemoryTranslator : TranslatorBase
    {
        [NotNull]
        private static readonly Uri _uri = new Uri("http://mymemory.translated.net/doc");

        public MyMemoryTranslator()
            : base("MyMemory", "MyMemory", _uri, GetCredentials())
        {
        }

        [NotNull]
        [ItemNotNull]
        private static IList<ICredentialItem> GetCredentials()
        {
            return new ICredentialItem[] { new CredentialItem("Key", "Key") };
        }

        [DataMember(Name = "Key")]
        [CanBeNull]
        public string SerializedKey
        {
            get => SaveCredentials ? Credentials[0].Value : null;
            set => Credentials[0].Value = value;
        }

        [CanBeNull]
        private string Key => Credentials[0].Value;

        public override void Translate(ITranslationSession translationSession)
        {
            foreach (var item in translationSession.Items)
            {
                if (translationSession.IsCanceled)
                    break;

                var translationItem = item;

                try
                {
                    var targetCulture = translationItem.TargetCulture.Culture ?? translationSession.NeutralResourcesLanguage;
                    var result = TranslateText(translationItem.Source, Key, translationSession.SourceLanguage, targetCulture);

                    translationSession.Dispatcher.BeginInvoke(() =>
                    {
                        if (result?.Matches != null)
                        {
                            foreach (var match in result.Matches)
                            {
                                var translation = match.Translation;
                                if (string.IsNullOrEmpty(translation))
                                    continue;

                                translationItem.Results.Add(new TranslationMatch(this, translation, match.Match.GetValueOrDefault() * match.Quality.GetValueOrDefault() / 100.0));
                            }
                        }
                        else
                        {
                            var translation = result?.ResponseData?.TranslatedText;
                            if (!string.IsNullOrEmpty(translation))
                                translationItem.Results.Add(new TranslationMatch(this, translation, result.ResponseData.Match.GetValueOrDefault()));
                        }
                    });
                }
                catch (Exception ex)
                {
                    translationSession.AddMessage(DisplayName + ": " + ex.Message);
                    break;
                }
            }
        }

        [CanBeNull]
        private static Response TranslateText([NotNull] string input, [CanBeNull] string key, [NotNull] CultureInfo sourceLanguage, [NotNull] CultureInfo targetLanguage)
        {
            var rawInput = RemoveKeyboardShortcutIndicators(input);

            var url = string.Format(CultureInfo.InvariantCulture,
                "http://api.mymemory.translated.net/get?q={0}!&langpair={1}|{2}",
                HttpUtility.UrlEncode(rawInput, Encoding.UTF8),
                sourceLanguage, targetLanguage);

            if (!string.IsNullOrEmpty(key))
                url += string.Format(CultureInfo.InvariantCulture, "&key={0}", HttpUtility.UrlEncode(key));

            var webRequest = (HttpWebRequest)WebRequest.Create(new Uri(url));
            webRequest.Proxy = WebProxy;

            using (var webResponse = webRequest.GetResponse())
            {
                var responseStream = webResponse.GetResponseStream();
                if (responseStream == null)
                    return null;

                using (var reader = new StreamReader(responseStream, Encoding.UTF8))
                {
                    var json = reader.ReadToEnd();
                    return JsonConvert.DeserializeObject<Response>(json);
                }
            }
        }

        [SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses")]
        [DataContract]
        private class ResponseData
        {
            [DataMember(Name = "translatedText")]
            [CanBeNull]
            public string TranslatedText
            {
                get;
                set;
            }

            [DataMember(Name = "match")]
            public double? Match
            {
                get;
                set;
            }
        }

        [SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses")]
        [DataContract]
        private class MatchData
        {
            [DataMember(Name = "translation")]
            [CanBeNull]
            public string Translation
            {
                get;
                set;
            }

            [DataMember(Name = "quality")]
            public double? Quality
            {
                get;
                set;
            }

            [DataMember(Name = "match")]
            public double? Match
            {
                get;
                set;
            }
        }

        [SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses")]
        [DataContract]
        private class Response
        {
            [DataMember(Name = "responseData")]
            [CanBeNull]
            public ResponseData ResponseData
            {
                get;
                set;
            }

            [DataMember(Name = "matches")]
            [CanBeNull]
            [ItemNotNull]
            public MatchData[] Matches
            {
                get;
                set;
            }


        }
    }
}