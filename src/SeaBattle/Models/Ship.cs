using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeaBattle.Models
{
    public class Ship
    {
        public int Size { get; set; }

        public int X { get; set; }

        public int Y { get; set; }

        public bool IsHorizontal { get; set; }

        public bool IsPlaced { get; set; }
    }
}
