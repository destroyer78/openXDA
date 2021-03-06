﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using GSF.Data.Model;

namespace openXDA.Model
{
    [TableName("Event")]
    public class Event
    {
        [Searchable(SearchType.LikeExpression)]
        [PrimaryKey(true)]
        public int ID { get; set; }
        public int FileGroupID { get; set; }
        public int MeterID { get; set; }
        public int LineID { get; set; }
        public int EventTypeID { get; set; }
        public int EventDataID { get; set; }
        public string Name { get; set; }
        public string Alias { get; set; }
        public string ShortName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int Samples { get; set; }
        public int TimeZoneOffset { get; set; }
        public int SamplesPerSecond { get; set; }
        public int SamplesPerCycle { get; set; }
        public string Description { get; set; }
        public string UpdatedBy { get; set; }
    }

    [TableName("EventView")]
    public class EventForDate: EventView { }

    [TableName("EventView")]
    public class EventForDay : EventView { }

    [TableName("EventView")]
    public class EventForMeter : EventView { }

    [TableName("EventView")]
    public class SingleEvent: EventView { }

    [TableName("EventView")]
    public class EventView
    {
        [Searchable(SearchType.LikeExpression)]
        [PrimaryKey(true)]
        public int ID { get; set; }
        public int FileGroupID { get; set; }
        public int MeterID { get; set; }
        public int LineID { get; set; }
        public int EventTypeID { get; set; }
        public int EventDataID { get; set; }
        public string Name { get; set; }
        public string Alias { get; set; }
        public string ShortName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int Samples { get; set; }
        public int TimeZoneOffset { get; set; }
        public int SamplesPerSecond { get; set; }
        public int SamplesPerCycle { get; set; }
        public string Description { get; set; }
        public string UpdatedBy { get; set; }

        [Searchable]
        public string LineName { get; set; }
        [Searchable]
        public string MeterName { get; set; }
        public string StationName { get; set; }
        public double Length { get; set; }
        [Searchable]
        public string EventTypeName { get; set; }
    }

    public class MeterEventsByLine
    {
        public int thelineid { get; set; }
        public int theeventid { get; set; }
        public string theeventtype { get; set; }
        public DateTime theinceptiontime { get; set; }
        public string thelinename { get; set; }
        public int voltage { get; set; }
        public string thefaulttype { get; set; }
        public string thecurrentdistance { get; set; }
        public bool pqiexists { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class FaultsDetailsByDate
    {
        public int thefaultid { get; set; }
        public string thesite { get; set; }
        public string locationname { get; set; }
        public int themeterid { get; set; }
        public int thelineid { get; set; }
        public int theeventid { get; set; }
        public string thelinename { get; set; }
        public int voltage { get; set; }
        public DateTime theinceptiontime { get; set; }
        public string thefaulttype { get; set; }
        public double thecurrentdistance { get; set; }
        public int notecount { get; set; }
        public int rk { get; set; }
        [NonRecordField]
        public string theeventtype { get; set; }

    }

}