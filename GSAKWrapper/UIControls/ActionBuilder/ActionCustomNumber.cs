﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace GSAKWrapper.UIControls.ActionBuilder
{
    public class ActionCustomNumber : ActionImplementationCondition
    {
        public const string STR_NAME = "CustomNumber";

        public enum Option
        {
            Value,
            IsEmpty
        }

        private double _value = 0.0;
        private Option _option = Option.Value;
        private string _userDataField = "";

        public ActionCustomNumber()
            : base(STR_NAME)
        {
        }

        public override SearchType SearchTypeTarget { get { return SearchType.Custom; } }

        public override UIElement GetUIElement()
        {
            if (Values.Count == 0)
            {
                Values.Add("0.0");
            }
            if (Values.Count < 2)
            {
                Values.Add(Option.Value.ToString());
            }
            if (Values.Count < 3)
            {
                Values.Add("");
            }
            StackPanel sp = new StackPanel();
            var opts2 = new string[] { Values[2] };
            ComboBox cb = CreateComboBox(opts2, Values[2]);
            cb.DropDownOpened += cb_DropDownOpened;
            cb.IsEditable = true;
            sp.Children.Add(cb);
            var opts = Enum.GetNames(typeof(Option));
            cb = CreateComboBox(opts, Values[1]);
            cb.IsEditable = false;
            sp.Children.Add(cb);
            TextBox tb = new TextBox();
            tb.HorizontalAlignment = HorizontalAlignment.Center;
            if (Values.Count == 0)
            {
                Values.Add("-");
            }
            tb.Text = Values[0];
            sp.Children.Add(tb);
            return sp;
        }

        void cb_DropDownOpened(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(Settings.Settings.Default.SelectedDatabase))
            {
                try
                {
                    var cb = sender as ComboBox;
                    var items = new List<ComboBoxItem>();
                    foreach (ComboBoxItem item in cb.Items)
                    {
                        if (item.Content as string != cb.Text)
                        {
                            items.Add(item);
                        }
                    }
                    foreach (var item in items)
                    {
                        cb.Items.Remove(item);
                    }
                    var fn = System.IO.Path.Combine(Settings.Settings.Default.DatabaseFolderPath, Settings.Settings.Default.SelectedDatabase, "sqlite.db3");
                    if (System.IO.File.Exists(fn))
                    {
                        using (var db = new Database.DBConSqlite(fn))
                        {
                            var cf = AvailableCustomFields(db);
                            foreach (var c in cf)
                            {
                                ComboBoxItem cboxitem = new ComboBoxItem();
                                cboxitem.Content = c;
                                cb.Items.Add(cboxitem);
                            }
                        }
                    }
                }
                catch
                {
                }
            }

        }
        public override void CommitUIData(UIElement uiElement)
        {
            StackPanel sp = uiElement as StackPanel;
            ComboBox cb = sp.Children[1] as ComboBox;
            Values[1] = cb.Text;
            TextBox tb = sp.Children[2] as TextBox;
            Values[0] = tb.Text;
            cb = sp.Children[0] as ComboBox;
            Values[2] = cb.Text;
        }
        public override bool PrepareRun(Database.DBCon db, string tableName)
        {
            _value = 0.0;
            _option = Option.Value;
            _userDataField = "";
            if (Values.Count > 0)
            {
                _value = Utils.Conversion.StringToDouble(Values[0]);
            }
            if (Values.Count > 1)
            {
                _option = (Option)Enum.Parse(typeof(Option), Values[1]);
            }
            if (Values.Count > 2)
            {
                _userDataField = Values[2];
                var cf = AvailableCustomFields(db);
                _userDataField = (from a in cf where string.Compare(a, _userDataField, true) == 0 select a).FirstOrDefault() ?? "";
            }
            return base.PrepareRun(db, tableName);
        }

        private List<string> AvailableCustomFields(Database.DBCon db)
        {
            var result = new List<string>();
            var dr = db.ExecuteReader("select fname from CustomLocal");
            while (dr.Read())
            {
                result.Add(dr.GetString(0));
            }
            return result;
        }

        public override void Process(Operator op, string inputTableName, string targetTableName)
        {
            if (!string.IsNullOrEmpty(_userDataField))
            {
                var fieldName = _userDataField;
                switch (_option)
                {
                    case Option.Value:
                        if (op == Operator.Equal)
                        {
                            SelectGeocachesOnWhereClause(inputTableName, targetTableName, string.Format("main.Custom.{0} = {1}", fieldName, _value.ToString(CultureInfo.InvariantCulture)), innerJoins: string.Format("inner join main.Custom on {0}.gccode=main.Custom.cCode", inputTableName));
                        }
                        else if (op == Operator.NotEqual)
                        {
                            SelectGeocachesOnWhereClause(inputTableName, targetTableName, string.Format("main.Custom.{0} <> {1}", fieldName, _value.ToString(CultureInfo.InvariantCulture)), innerJoins: string.Format("inner join main.Custom on {0}.gccode=main.Custom.cCode", inputTableName));
                        }
                        else if (op == Operator.Larger)
                        {
                            SelectGeocachesOnWhereClause(inputTableName, targetTableName, string.Format("main.Custom.{0} > {1}", fieldName, _value.ToString(CultureInfo.InvariantCulture)), innerJoins: string.Format("inner join main.Custom on {0}.gccode=main.Custom.cCode", inputTableName));
                        }
                        else if (op == Operator.LargerOrEqual)
                        {
                            SelectGeocachesOnWhereClause(inputTableName, targetTableName, string.Format("main.Custom.{0} >= {1}", fieldName, _value.ToString(CultureInfo.InvariantCulture)), innerJoins: string.Format("inner join main.Custom on {0}.gccode=main.Custom.cCode", inputTableName));
                        }
                        else if (op == Operator.Less)
                        {
                            SelectGeocachesOnWhereClause(inputTableName, targetTableName, string.Format("main.Custom.{0} < {1}", fieldName, _value.ToString(CultureInfo.InvariantCulture)), innerJoins: string.Format("inner join main.Custom on {0}.gccode=main.Custom.cCode", inputTableName));
                        }
                        else if (op == Operator.LessOrEqual)
                        {
                            SelectGeocachesOnWhereClause(inputTableName, targetTableName, string.Format("main.Custom.{0} <= {1}", fieldName, _value.ToString(CultureInfo.InvariantCulture)), innerJoins: string.Format("inner join main.Custom on {0}.gccode=main.Custom.cCode", inputTableName));
                        }
                        break;
                    case Option.IsEmpty:
                        if (op == Operator.Equal)
                        {
                            SelectGeocachesOnWhereClause(inputTableName, targetTableName, string.Format("main.Custom.{0} is NULL", fieldName), innerJoins: string.Format("inner join main.Custom on {0}.gccode=main.Custom.cCode", inputTableName));
                        }
                        else if (op == Operator.NotEqual)
                        {
                            SelectGeocachesOnWhereClause(inputTableName, targetTableName, string.Format("main.Custom.{0} is not NULL", fieldName), innerJoins: string.Format("inner join main.Custom on {0}.gccode=main.Custom.cCode", inputTableName));
                        }
                        break;
                }
            }
        }

    }
}
