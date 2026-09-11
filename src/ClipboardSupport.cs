using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows;

namespace ArkBoard
{
    [DataContract]
    internal sealed class ClipboardImageData
    {
        [DataMember] internal ImageItem Item;
        [DataMember] internal byte[] Bytes;
    }

    internal static class ArkBoardClipboard
    {
        internal const string Format = "ArkBoard.Image.v1";

        internal static DataObject Create(ImageItem item, AssetData asset)
        {
            var payload = new ClipboardImageData { Item = item.Copy(), Bytes = asset.Bytes };
            string json;
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(ClipboardImageData)).WriteObject(stream, payload);
                json = Encoding.UTF8.GetString(stream.ToArray());
            }
            var data = new DataObject();
            data.SetData(Format, json); data.SetImage(asset.BitmapFor(item)); return data;
        }

        internal static bool TryRead(IDataObject data, out ImageItem item, out AssetData asset)
        {
            item = null; asset = null;
            try
            {
                if (data == null || !data.GetDataPresent(Format)) return false;
                string json = data.GetData(Format) as string;
                if (string.IsNullOrEmpty(json) || json.Length > 150 * 1024 * 1024) return false;
                ClipboardImageData payload;
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json), false))
                    payload = (ClipboardImageData)new DataContractJsonSerializer(typeof(ClipboardImageData)).ReadObject(stream);
                if (payload == null || payload.Item == null || payload.Item.IsText || payload.Bytes == null ||
                    payload.Item.Width < .01 || payload.Item.Height < .01 || payload.Item.Width > 1000000 || payload.Item.Height > 1000000 ||
                    !BoardDocument.Finite(payload.Item.Width) || !BoardDocument.Finite(payload.Item.Height) ||
                    !BoardDocument.ValidMask(payload.Item)) return false;
                asset = AssetData.Create(payload.Bytes); item = payload.Item; item.Asset = asset.Key;
                if (item.LayerVisibility != null && (asset.Psd == null || item.LayerVisibility.Count != asset.Psd.Layers.Count)) return false;
                if (asset.Psd != null && item.LayerVisibility == null) item.LayerVisibility = asset.Psd.Layers.Select(l => l.DefaultVisible).ToList();
                return true;
            }
            catch { item = null; asset = null; return false; }
        }
    }
}
