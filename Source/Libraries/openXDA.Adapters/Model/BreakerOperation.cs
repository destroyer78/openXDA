﻿using System;

namespace openXDA.Adapters.Model
{
    public class BreakerOperation
    {
        public int ID { get; set; }
        public int EventID { get; set; }
        public int PhaseID { get; set; }
        public int BreakerOperationTypeID { get; set; }
        public int BreakerNumber { get; set; }
        public DateTime TripCoilEnergized { get; set; }
        public DateTime StatusBitSet { get; set; }
        public DateTime APhaseCleared { get; set; }
        public DateTime BPhaseCleared { get; set; }
        public DateTime CPhaseCleared { get; set; }
        public double BreakerTiming { get; set; }
        public double APhaseBreakerTiming { get; set; }
        public double BPhaseBreakerTiming { get; set; }
        public double CPhaseBreakerTiming { get; set; }
        public int BreakerSpeed { get; set; }
    }
}
