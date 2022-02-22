using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Magrathea.CustomNode
{
    public class SpawnPoint : RPComponent
    {
        public override string Type => base.Type + ".spawn-point";

        public override JProperty Serialized => new JProperty("extras", new JObject(
            new JProperty(Type, new JObject()),
            new JProperty("customnode.entity", transform.name)
        ));
    }

}
