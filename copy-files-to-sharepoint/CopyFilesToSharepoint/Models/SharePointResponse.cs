using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace CopyFilesToSharePoint.Models
{
    public class SharePointResponse
    {
        public class Content
        {
            [JsonProperty("m:properties")]
            public MProperties Mproperties { get; set; }
        }

        public class Entry
        {
            public Content Content { get; set; }
        }

        public class Link
        {
            [JsonProperty("@href")]
            public string Href { get; set; }
            [JsonProperty("@rel")]
            public string Rel { get; set; }
        }

        public class MProperties
        {
            [JsonProperty("d:Created")]
            public DCreated DCreated { get; set; }
            [JsonProperty("d:Modified")]
            public DModified DModified { get; set; }
        }

        public class DModified
        {
            [JsonProperty("#text")]
            public DateTime text { get; set; }
        }

        public class DCreated
        {
            [JsonProperty("#text")]
            public DateTime text { get; set; }
        }

        public class Root
        {
            public Entry entry { get; set; }
        }

        public class FileRefRoot
        {
            public FileRefEntry Entry { get; set; }
        }

        public class FileRefEntry
        {
            public List<Link> Link { get; set; }
        }
    }
}
