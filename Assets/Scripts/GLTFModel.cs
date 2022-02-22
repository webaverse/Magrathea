using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Magrathea.CustomNode
{
    public class GLTFModel : RPComponent
    {
        public override string Type => base.Type + ".gltf-model";

        public override JProperty Serialized => new JProperty("extras", new JObject(
            new JProperty(Type, new JObject()),
            new JProperty("customnode.entity", transform.name)
        ));
    }

}
