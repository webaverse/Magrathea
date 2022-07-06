using System.Collections.Generic;
using System.IO;
using System;

using Newtonsoft.Json.Linq;

namespace Magrathea
{
    public  class  GLTFToGLBConverter
    {
        string rootPath = "";
        string fileName = "";

        JObject gltf;
        int bufferOffset = 0;
        List<Stream> outputBuffers = new List<Stream>();
        Dictionary<int, long> bufferMap = new Dictionary<int, long>();

        Action<string> OnFinished;

        public void ConvertToGLB(string path, Action<string> callback = null)
        {
            OnFinished = callback;
                traverseFileTree(path + ".gltf");
        }

        private void traverseFileTree(string path)
        {
            string extension = Path.GetExtension(path);
            rootPath = Path.GetDirectoryName(path);
            string text = System.IO.File.ReadAllText(path);
            fileName = Path.GetFileNameWithoutExtension(path);
            gltf = JObject.Parse(text);


            bufferOffset = 0;

            // handle buffer changes
            ProcessBuffers();

            // handle changes to images
            ProcessImages();

            // Handle changes to BufferViews
            ProcessBufferViews();

            UpdateBuffersLength();

            // Package into GLB
            CreatGLB();

            UnityEngine.Debug.Log("Done traversing for " + path);
        }


        private  void  ProcessBuffers ()
        {
            JArray buffers = (JArray)gltf["buffers"];

            for (int index = 0; index < buffers.Count; index++)
            {
                Stream data = GetStream((JObject)buffers[index]);

                if (data != null)
                {
                    outputBuffers.Add(data);
                }

                bufferMap.Add(index, bufferOffset);
                bufferOffset += AlignedLength(data.Length);

                ((JObject)buffers[index]).Property("uri").Remove();
                ((JObject)buffers[index])["byteLength"] = data.Length;
            }
        }

        private  void  ProcessImages ()
        {
            if (gltf.Property("images") == null)
                return;

            JArray images = (JArray)gltf["images"];
            int bufferIndex = ((JArray)gltf["buffers"]).Count;

            foreach (var image in images)
            {
                Stream data = GetStream((JObject)image);

                if (data != null)
                {
                    JObject  bufferView  =  new  JObject ();
                    bufferView.Add(new JProperty("buffer", 0));
                    bufferView.Add(new JProperty("byteOffset", bufferOffset));
                    bufferView.Add(new JProperty("byteLength", data.Length));

                    bufferMap.Add(bufferIndex, bufferOffset);
                    bufferIndex++;
                    bufferOffset += AlignedLength(data.Length);

                    JArray bufferViews = (JArray)gltf["bufferViews"];
                    bufferViews.Add(bufferView);

                    JObject bufferViewsJSON = new JObject();

                    outputBuffers.Add(data);

                    if (((JObject)image).Property("bufferView") == null)
                        ((JObject)image).Add(new JProperty("bufferView", bufferViews.Count - 1));

                    if (((JObject)image).Property("mimeType") == null)
                        ((JObject)image).Add(new JProperty("mimeType", getMimeType((JObject)image)));
                }

                ((JObject)image).Property("uri").Remove();
            }
        }

        private  void  ProcessBufferViews ()
        {
            JArray bufferViews = (JArray)gltf["bufferViews"];
            if(bufferViews != null && bufferViews.HasValues)
            foreach (var bufferView in bufferViews)
            {
                if (((JObject)bufferView).Property("byteOffset") == null)
                {
                    ((JObject)bufferView).Add(new JProperty("byteOffset", 0));
                }
                else
                {
                    long shift = (long)(((JObject)bufferView)["byteOffset"]) + bufferMap[(int)(((JObject)bufferView)["buffer"])];
                    ((JObject)bufferView)["byteOffset"] = shift;
                }

                ((JObject)bufferView)["buffer"] = 0;
            }

        }

        private void UpdateBuffersLength()
        {

            JArray buffers = (JArray)gltf["buffers"];

            foreach (var buffer in buffers)
            {
                ((JObject)buffer)["byteLength"] = bufferOffset;
            }
        }

        private  void  CreatGLB ()
        {
            const uint MAGIC_NUMBER = 0x46546c67;
            string jsonBuffer = gltf.ToString(Newtonsoft.Json.Formatting.None);
            int jsonAlignedLength = AlignedLength(jsonBuffer.Length);
            int padding = 0;

            if (jsonAlignedLength != jsonBuffer.Length)
            {
                padding = jsonAlignedLength - jsonBuffer.Length;
            }
            int binBufferSize = bufferOffset;
            int totalSize = 12 + // file header: magic + version + length
            8 + // json chunk header: json length + type
            jsonAlignedLength +
            8 + // bin chunk header: chunk length + type
            binBufferSize;

            FileStream glbFile = null;
            string glbURL = Path.Combine(rootPath, fileName + ".glb");

            if (File.Exists(glbURL))
            {
                File.Delete(glbURL);
                glbFile = File.Create(glbURL);
            }
            else
                glbFile = File.Create(glbURL);


            BinaryWriter glbWriter = new BinaryWriter(glbFile);
            glbWriter.Write(BitConverter.GetBytes(MAGIC_NUMBER));
            glbWriter.Write(BitConverter.GetBytes(0x00000002));
            glbWriter.Write(BitConverter.GetBytes(totalSize));
            glbWriter.Write(BitConverter.GetBytes(jsonAlignedLength));
            glbWriter.Write(BitConverter.GetBytes(0x4E4F534A));

            for (int index = 0; index < jsonBuffer.Length; index++)
            {
                glbWriter.Write(Convert.ToByte(jsonBuffer[index]));
            }

            for (int index = 0; index < padding; index++)
            {
                glbWriter.Write(Convert.ToByte(0x20));
            }

            glbWriter.Write(BitConverter.GetBytes(binBufferSize));
            glbWriter.Write(BitConverter.GetBytes(0x004E4942));

            for (int index = 0; index < outputBuffers.Count; index++)
            {
                BinaryReader binary = new BinaryReader(outputBuffers[index]);
                byte[] byteData = binary.ReadBytes((int)outputBuffers[index].Length);


                int byteDataLength = AlignedLength(byteData.Length);
                int byteDataPadding = 0;

                if (byteDataLength != byteData.Length)
                {
                    byteDataPadding = byteDataLength - byteData.Length;
                }

                for (int j = 0; j < byteData.Length; j++)
                {
                    glbWriter.Write(Convert.ToByte(byteData[j]));
                }

                for (int j = 0; j < byteDataPadding; j++)
                {
                    glbWriter.Write(Convert.ToByte(0x20));
                }
            }

            glbWriter.Close();
            glbFile.Close();

            OnFinished?.Invoke(glbURL);
        }

        private Stream GetStream(JObject jObject)
        {
            if (jObject.Property("uri") == null)
            {
                return null;
            }
            else if (isBase64((string)jObject["uri"]))
            {
                return decodeBase64((string)jObject["uri"]);
            }
            else
            {
                Stream LoadedStream;
                string uri = (string)jObject["uri"];
                string filename = uri.Substring(uri.LastIndexOf('/') + 1);

                string pathToLoad = Path.Combine(rootPath, uri);
                LoadedStream = File.OpenRead(pathToLoad);
                return LoadedStream;
            }
        }

        private  bool  isBase64 ( string  uri )
        {
            return uri.Length < 5 ? false : uri.Substring(0, 5) == "data:";
        }

        private Stream decodeBase64(string uri)
        {
            string base64url = uri.Substring(uri.LastIndexOf(',') + 1);
            byte[] imageBytes = Convert.FromBase64String(base64url);

            Stream LoadedStream;
            LoadedStream = new MemoryStream(imageBytes);
            return LoadedStream;
        }

        private int AlignedLength(long length)
        {
            long byteLength = 4;

            if (length == 0)
                return 0;

            long space = length % byteLength;

            if (space == 0)
                return (int)(length);

            return (int)(length + (byteLength - space));
        }

        private string getMimeType(JObject jObject)
        {
            string uri = (string)jObject["uri"];
            string extension = Path.GetExtension(uri);
            string mimeType = "";

            switch (extension)
            {
                case ".png":
                    mimeType = "image/png";
                    break;
                case ".jpg":
                case ".jpeg":
                    mimeType = "image/jpeg";
                    break;
                case ".glsl":
                case ".vert":
                case ".vs":
                case ".frag":
                case ".fs":
                case ".txt":
                    mimeType = "text/plain";
                    break;
                case ".dds":
                    mimeType = "image/vnd-ms.dds";
                    break;
                default:
                    mimeType = "image/png";
                    break;
            }
            return mimeType;
        }

    }
}