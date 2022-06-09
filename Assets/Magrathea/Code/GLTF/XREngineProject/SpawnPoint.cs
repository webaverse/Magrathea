using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Magrathea.XREngineProject
{
    public class SpawnPoint : RPComponent
    {
        public override string Type => base.Type + ".spawn-point";

        public override JProperty Serialized => new JProperty("extras", new JObject(
            new JProperty(Type, new JObject()),
            new JProperty("Magrathea.entity", transform.name)
        ));
    }

}
