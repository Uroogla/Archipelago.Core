using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.Models
{
    public class CompositeLocation : ILocation
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public LocationCheckType CheckType { get; set; }
        public List<ILocation> Conditions { get; set; } = new List<ILocation>();

        public bool Check()
        {
            if (Conditions == null || !Conditions.Any())
            {
                return true;
            }

            return CheckType switch
            {
                LocationCheckType.AND => CheckAll(),
                LocationCheckType.OR => CheckAny(),
                _ => throw new NotSupportedException($"Logical operator type '{CheckType}' is not supported.")
            };
        }

        private bool CheckAll()
        {
            foreach (var condition in Conditions)
            {
                if (!condition.Check())
                {
                    return false;
                }
            }
            return true;
        }

        private bool CheckAny()
        {
            foreach (var condition in Conditions)
            {
                if (condition.Check())
                {
                    return true;
                }
            }
            return false;
        }
    }
}
