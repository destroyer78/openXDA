﻿//******************************************************************************************************
//  TrendingDataSummaryOperation.cs - Gbtc
//
//  Copyright © 2015, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may
//  not use this file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  05/05/2015 - J. Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using FaultData.Database;
using FaultData.DataOperations;
using FaultData.DataResources;
using FaultData.DataSets;
using GSF;
using GSF.Configuration;
using GSF.Snap.Services;
using log4net;
using openHistorian.Net;
using openHistorian.Queues;
using openHistorian.Snap;

namespace openHistorian.XDALink
{
    /// <summary>
    /// Series ID values.
    /// </summary>
    public enum SeriesID
    {
        Minimum = 0,
        Maximum = 1,
        Average = 2
    }

    /// <summary>
    /// Data operation to load trending data into the openHistorian.
    /// </summary>
    public class TrendingDataSummaryOperation : DataOperationBase<MeterDataSet>
    {
        #region [ Members ]

        // Fields
        private HistorianSettings m_historianSettings;

        #endregion

        #region [ Constructors ]

        /// <summary>
        /// Creates a new <see cref="TrendingDataSummaryOperation"/>.
        /// </summary>
        public TrendingDataSummaryOperation()
        {
            m_historianSettings = new HistorianSettings();
        }

        #endregion

        #region [ Properties ]

        [Category]
        [SettingName("Historian")]
        public HistorianSettings HistorianSettings
        {
            get
            {
                return m_historianSettings;
            }
        }

        #endregion
        
        #region [ Methods ]

        public override void Prepare(DbAdapterContainer dbAdapterContainer)
        {
            // Not doing anything with database
        }

        public override void Load(DbAdapterContainer dbAdapterContainer)
        {
            // Not doing anything with database
        }

        public override void Execute(MeterDataSet dataSet)
        {
            Log.Info("Executing operation to load trending summary data into the openHistorian...");

            using (HistorianClient client = new HistorianClient(m_historianSettings.HostName, m_historianSettings.Port))
            using (ClientDatabaseBase<HistorianKey, HistorianValue> database = client.GetDatabase<HistorianKey, HistorianValue>(m_historianSettings.InstanceName))
            using (HistorianInputQueue queue = new HistorianInputQueue(() => database))
            {
                Dictionary<Channel, List<TrendingDataSummaryResource.TrendingDataSummary>> trendingDataSummaries = dataSet.GetResource<TrendingDataSummaryResource>().TrendingDataSummaries;
                HistorianKey key = new HistorianKey();
                HistorianValue value = new HistorianValue();

                foreach (KeyValuePair<Channel, List<TrendingDataSummaryResource.TrendingDataSummary>> channelSummaries in trendingDataSummaries)
                {
                    // Reduce data set to valid summaries
                    IEnumerable<TrendingDataSummaryResource.TrendingDataSummary> validSummaries = channelSummaries.Value.Where(summary => summary.IsValid);
                    uint channelID = (uint)channelSummaries.Key.ID;

                    foreach (TrendingDataSummaryResource.TrendingDataSummary summary in validSummaries)
                    {
                        key.TimestampAsDate = summary.Time;

                        // Write minimum series value
                        key.PointID = Word.MakeQuadWord(channelID, (uint)SeriesID.Minimum);
                        value.AsSingle = (float)summary.Minimum;
                        queue.Enqueue(key, value);

                        // Write maximum series value
                        key.PointID = Word.MakeQuadWord(channelID, (uint)SeriesID.Maximum);
                        value.AsSingle = (float)summary.Maximum;
                        queue.Enqueue(key, value);

                        // Write average series value
                        key.PointID = Word.MakeQuadWord(channelID, (uint)SeriesID.Average);
                        value.AsSingle = (float)summary.Average;
                        queue.Enqueue(key, value);
                    }
                }

                // Wait for queue to be processed
                while (queue.Size > 0)
                    Thread.Sleep(1000);
            }
        }

        #endregion

        #region [ Static ]

        // Static Fields
        private static readonly ILog Log = LogManager.GetLogger(typeof(TrendingDataSummaryOperation));

        #endregion        
    }
}
