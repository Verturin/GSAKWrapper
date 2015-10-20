﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Collections;

namespace GSAKWrapper.UIControls.ActionBuilder
{
    public class ActionImplementation
    {
        [Flags]
        public enum Operator : int
        {
            Equal = 1,
            NotEqual = 2,
            LessOrEqual = 4,
            Less = 8,
            LargerOrEqual = 16,
            Larger = 32,
        }

        public class OutputConnectionInfo
        {
            public Operator OutputOperator { get; set; }
            public ActionImplementation ConnectedAction { get; set; }
            public int PassCounter { get; set; }
        }

        protected const string TempTableName = "gskwrp_tmp";
        protected const string TempTableName2 = "gskwrp_tmp2";

        public string Name { get; private set; }
        public string ID { get; set; }
        public Point Location { get; set; }
        private ActionControl _assignedActionControl = null;
        public List<string> Values { get; private set; }
        public long TotalGeocachesAtInput { get; private set; }
        public System.Diagnostics.Stopwatch TotalProcessTime { get; private set; }

        //connections
        private List<OutputConnectionInfo> _outputConnectionInfo = null;

        protected Database.DBCon DatabaseConnection { get; private set; }
        protected string AssignedTableName { get; private set; }
        private List<string> _createdTables;

        public ActionImplementation(string name)
        {
            Name = name;
            _outputConnectionInfo = new List<OutputConnectionInfo>();
            Values = new List<string>();
            _createdTables = new List<string>();
            TotalProcessTime = new System.Diagnostics.Stopwatch();
        }

        protected void CreateTableInDatabase(string tableName, bool dropIfExists = false, bool emptyIfExists = true)
        {
            if (!_createdTables.Contains(tableName))
            {
                _createdTables.Add(tableName);
            }
            if (DatabaseConnection.TableExists(tableName))
            {
                if (dropIfExists)
                {
                    DatabaseConnection.ExecuteNonQuery(string.Format("drop table {0}", tableName));
                }
                else
                {
                    if (emptyIfExists)
                    {
                        DatabaseConnection.ExecuteNonQuery(string.Format("delete from {0}", tableName));
                    }
                    return;
                }
            }
            DatabaseConnection.ExecuteNonQuery(string.Format("create table '{0}' (gccode text)", tableName));
        }

        public virtual bool PrepareRun(Database.DBCon db, string tableName)
        {
            DatabaseConnection = db;
            AssignedTableName = tableName;
            CreateTableInDatabase(ActionInputTableName);
            foreach (OutputConnectionInfo oci in _outputConnectionInfo)
            {
                oci.PassCounter = 0;
            }
            TotalProcessTime.Reset();
            return true;
        }
        public virtual void FinalizeRun()
        {
            foreach (var t in _createdTables)
            {
                DatabaseConnection.ExecuteNonQuery(string.Format("drop table {0}", t));
            }
            _createdTables.Clear();
        }

        public string ActionInputTableName
        {
            get
            {
                return string.Format("{0}_inp", AssignedTableName);
            }
        }

        public string ConnectorOutputTableName(Operator op)
        {
            return string.Format("{0}_{1}", AssignedTableName, op.ToString());
        }

        public void Run(string inputTableName)
        {
            //get input list
            CreateTableInDatabase(ActionInputTableName, emptyIfExists: false);
            if (string.IsNullOrEmpty(inputTableName))
            {
                DatabaseConnection.ExecuteNonQuery(string.Format("insert into {0} select distinct Code as gccode from Caches", ActionInputTableName));
            }
            else
            {
                if ((long)DatabaseConnection.ExecuteScalar(string.Format("select count(1) from {0}", ActionInputTableName)) == 0)
                {
                    DatabaseConnection.ExecuteNonQuery(string.Format("insert into {0} select * from {1}", ActionInputTableName, inputTableName));
                }
                else
                {
                    DatabaseConnection.ExecuteNonQuery(string.Format("insert into {0} select * from {1}", TempTableName2, ActionInputTableName)); //todo: in one sql statement
                    DatabaseConnection.ExecuteNonQuery(string.Format("insert into {0} select * from {1}", TempTableName2, inputTableName));
                    DatabaseConnection.ExecuteNonQuery(string.Format("delete from {0}", ActionInputTableName));
                    DatabaseConnection.ExecuteNonQuery(string.Format("insert into {0} select distinct * from {1}", ActionInputTableName, TempTableName2));
                }
            }
            TotalGeocachesAtInput = (long)DatabaseConnection.ExecuteScalar(string.Format("select count(1) from {0}", ActionInputTableName));

            List<string> processedOps = new List<string>();
            foreach (var c in _outputConnectionInfo)
            {
                if (c.ConnectedAction != null)
                {
                    string connectorTable = ConnectorOutputTableName(c.OutputOperator);
                    if (!processedOps.Contains(connectorTable))
                    {
                        TotalProcessTime.Start();
                        CreateTableInDatabase(connectorTable);
                        if ((long)DatabaseConnection.ExecuteScalar(string.Format("select count(1) from {0}", connectorTable)) == 0)
                        {
                            DatabaseConnection.ExecuteNonQuery(string.Format("delete from {0}", connectorTable));
                            Process(c.OutputOperator, inputTableName, connectorTable);
                        }
                        else
                        {
                            DatabaseConnection.ExecuteNonQuery(string.Format("delete from {0}", TempTableName));
                            DatabaseConnection.ExecuteNonQuery(string.Format("delete from {0}", TempTableName2));
                            Process(c.OutputOperator, inputTableName, TempTableName);
                            DatabaseConnection.ExecuteNonQuery(string.Format("insert into {0} select * from {1}", TempTableName2, TempTableName)); //todo: in one sql statement
                            DatabaseConnection.ExecuteNonQuery(string.Format("insert into {0} select * from {1}", TempTableName2, connectorTable));
                            DatabaseConnection.ExecuteNonQuery(string.Format("insert into {0} select distinct * from {1}", connectorTable, TempTableName2));
                        }
                        TotalProcessTime.Stop();
                        processedOps.Add(connectorTable);
                        c.PassCounter = (int)(long)DatabaseConnection.ExecuteScalar(string.Format("select count(1) from {0}", connectorTable));
                    }
                    else
                    {
                    }
                    c.ConnectedAction.Run(connectorTable);
                }
            }
        }

        public virtual void Process(Operator op, string inputTableName, string targetTableName)
        {
            //e.g. insert into targetTableName select Code as gccode from Caches inner join inputTableName on gccode.Code = inputTableName.gccode where Name like '%w%'
        }

        public bool ConnectToOutput(ActionImplementation impl, Operator op)
        {
            if ((from c in _outputConnectionInfo where c.ConnectedAction == impl && c.OutputOperator == op select c).FirstOrDefault() == null)
            {
                OutputConnectionInfo oci = new OutputConnectionInfo();
                oci.OutputOperator = op;
                oci.ConnectedAction = impl;
                _outputConnectionInfo.Add(oci);
                return true;
            }
            else
            {
                return false;
            }
        }

        public void SelectedLanguageChanged()
        {
            if (_assignedActionControl != null)
            {
                _assignedActionControl.Title.Content = Localization.TranslationManager.Instance.Translate(Name);
            }
        }

        public List<ActionImplementation> GetOutputConnections(Operator op)
        {
            return ((from c in _outputConnectionInfo where c.OutputOperator == op select c.ConnectedAction).ToList());
        }

        public int GetOutputConnectorPassCounter(Operator op)
        {
            return ((from c in _outputConnectionInfo where c.OutputOperator == op select c.PassCounter).FirstOrDefault());
        }

        public List<OutputConnectionInfo> GetOutputConnections()
        {
            return _outputConnectionInfo;
        }

        public void RemoveOutputConnection(ActionImplementation impl)
        {
            List<OutputConnectionInfo> ocil = (from c in _outputConnectionInfo where c.ConnectedAction == impl select c).ToList();
            foreach (var oci in ocil)
            {
                _outputConnectionInfo.Remove(oci);
            }
        }
        public void RemoveOutputConnection(ActionImplementation impl, Operator op)
        {
            var oci = (from c in _outputConnectionInfo where c.ConnectedAction == impl && c.OutputOperator == op select c).FirstOrDefault();
            if (oci != null)
            {
                _outputConnectionInfo.Remove(oci);
            }
        }

        public ActionControl UIActionControl
        {
            get { return _assignedActionControl; }
            set { _assignedActionControl = value; }
        }

        public virtual UIElement GetUIElement()
        {
            return null;
        }

        public virtual void CommitUIData(UIElement uiElement)
        {
        }

        public virtual bool AllowEntryPoint
        {
            get { return true; }
        }

        public virtual Operator AllowOperators
        {
            get { return Operator.Equal | Operator.Larger | Operator.LargerOrEqual | Operator.Less | Operator.LessOrEqual | Operator.NotEqual; }
        }

        public ComboBox CreateComboBox(string[] items, string value)
        {

            ComboBox cb = new ComboBox();
            cb.Width = 150;
            if (items != null)
            {
                foreach (string s in items)
                {
                    ComboBoxItem cboxitem = new ComboBoxItem();
                    cboxitem.Content = s;
                    cb.Items.Add(cboxitem);
                }
            }
            cb.HorizontalAlignment = HorizontalAlignment.Center;
            cb.IsEditable = true;
            cb.IsSynchronizedWithCurrentItem = false;
            cb.IsEnabled = true;
            if (Values.Count == 0)
            {
                Values.Add("");
            }
            cb.Text = value;
            return cb;
        }

        public static Operator GetOperators(string sGC, string sV)
        {
            return GetOperators(sGC.CompareTo(sV));
        }

        public static Operator GetOperators(int cmp)
        {
            Operator result = 0;
            if (cmp == 0)
            {
                result |= Operator.Equal;
                result |= Operator.LargerOrEqual;
                result |= Operator.LessOrEqual;
            }
            else
            {
                result |= Operator.NotEqual;
                if (cmp < 0)
                {
                    result |= Operator.Less;
                    result |= Operator.LessOrEqual;
                }
                else
                {
                    result |= Operator.Larger;
                    result |= Operator.LargerOrEqual;
                }
            }
            return result;
        }
    }

}