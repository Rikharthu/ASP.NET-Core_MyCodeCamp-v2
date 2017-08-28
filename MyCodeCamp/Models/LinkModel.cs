using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyCodeCamp.Models
{
    public class LinkModel
    {
        public string Href { get; set; } // url
        public string Rel { get; set; } // purpose of the link
        public string Verb { get; set; } = "GET";
    }
}