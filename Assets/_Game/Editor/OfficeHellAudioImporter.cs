using UnityEditor;
using UnityEngine;

namespace OfficeHell.EditorTools
{
    /// <summary>
    /// Keeps latency-sensitive one-shots decoded while long-lived clips trade a small amount of
    /// CPU for memory. Folder ownership makes newly delivered clips inherit the same contract.
    /// </summary>
    public sealed class OfficeHellAudioImporter : AssetPostprocessor
    {
        const string Root = "Assets/_Game/Audio/";

        public override uint GetVersion()
        {
            return 3;
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Root, System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            AudioImporter importer = assetImporter as AudioImporter;
            if (importer == null)
            {
                return;
            }

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;

            if (assetPath.IndexOf("/BGM/", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                importer.forceToMono = false;
                importer.loadInBackground = true;
                settings.preloadAudioData = false;
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.65f;
            }
            else if (assetPath.IndexOf("/Drop/", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                importer.forceToMono = false;
                importer.loadInBackground = false;
                settings.preloadAudioData = true;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.80f;
            }
            else if (assetPath.IndexOf("/Loop/", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                importer.forceToMono = true;
                importer.loadInBackground = false;
                settings.preloadAudioData = false;
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.70f;
            }
            else
            {
                importer.forceToMono = true;
                importer.loadInBackground = false;
                settings.preloadAudioData = true;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.quality = 1f;
            }

            importer.defaultSampleSettings = settings;
        }
    }
}
