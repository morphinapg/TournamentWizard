using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace TournamentWizard.ViewModels
{
    [DataContract]
    internal class Tier
    {
        [DataMember]
        public List<string> Inputs, Outputs = new();
        [DataMember]
        public int CurrentPosition = 0;

        public int NumberOfChoices => Inputs.Count / 2;

        public Tier(List<string> inputs, bool replacement = false)
        {
            var r = Random.Shared;
            Inputs = replacement ?
                inputs :
                inputs.Select(x => new {index = r.NextDouble(), item = x}).OrderBy(x => x.index).Select(x => x.item).Distinct().ToList();
        }

        public string[] GetNext()
        {          
            if (Inputs.Count > CurrentPosition + 1)
                return new string[] { Inputs[CurrentPosition], Inputs[CurrentPosition + 1] };

            if (Inputs.Count > CurrentPosition)
                return new string[] { Inputs[CurrentPosition] };

            return new string[0];
        }
    }
}
