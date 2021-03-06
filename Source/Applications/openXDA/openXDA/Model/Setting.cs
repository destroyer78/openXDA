﻿using System.ComponentModel.DataAnnotations;
using GSF.Data.Model;

namespace openXDA.Model
{
    public class Setting
    {
        [PrimaryKey(true)]
        public int ID { get; set; }
        [Searchable]
        public string Name { get; set; }
        [Searchable]
        public string Value { get; set; }
        [Searchable]
        public string DefaultValue { get; set; }
         
    }

    [TableName("DashSettings")]
    public class DashSettings
    {
        [PrimaryKey(true)]
        public int ID { get; set; }

        [StringLength(500)]
        [Searchable]
        public string Name { get; set; }

        [StringLength(500)]
        [Searchable]
        public string Value { get; set; }

        public bool Enabled { get; set; }

    }

}