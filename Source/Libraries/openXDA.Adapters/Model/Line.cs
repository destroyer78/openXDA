﻿using GSF.Data.Model;

namespace openXDA.Adapters.Model
{
    public class Line
    {
        [PrimaryKey(true)]
        public int ID { get; set; }

        public string AssetKey { get; set; }

        public float VoltageKV { get; set; }

        public float ThermalRating { get; set; }

        public float Length { get; set; }

        public string Description { get; set; }
    }
}
