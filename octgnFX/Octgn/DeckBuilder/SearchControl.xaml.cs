﻿using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Octgn.Data;
using System.Text;

namespace Octgn.DeckBuilder
{
    public partial class SearchControl
    {
        public SearchControl(Data.Game game)
        {
            Game = game;
            InitializeComponent();
            filtersList.ItemsSource =
                Enumerable.Repeat<object>("First", 1).Union(
                    Enumerable.Repeat<object>(new SetPropertyDef(Game.Sets), 1).Union(
                        game.AllProperties.Where(p => !p.Hidden)));
            GenerateColumns(game);
            resultsGrid.ItemsSource = game.SelectCards(null).DefaultView;
        }

        public int SearchIndex { get; set; }

        public string SearchName
        {
            get { return "Search #" + SearchIndex; }
        }

        public Data.Game Game { get; private set; }
        public event EventHandler<SearchCardIdEventArgs> CardRemoved , CardAdded;
        public event EventHandler<SearchCardImageEventArgs> CardSelected;

        private void ResultKeyDownHandler(object sender, KeyEventArgs e)
        {
            var row = (DataRowView) resultsGrid.SelectedItem;
            if (row == null) return;

            switch (e.Key)
            {
                case Key.Insert:
                case Key.Add:
                case Key.Enter:
                    if (CardAdded != null)
                        CardAdded(this, new SearchCardIdEventArgs {CardId = Guid.Parse(row["id"] as string)});
                    e.Handled = true;
                    break;

                case Key.Delete:
                case Key.Subtract:
                    if (CardRemoved != null)
                        CardRemoved(this, new SearchCardIdEventArgs {CardId = Guid.Parse(row["id"] as string)});
                    e.Handled = true;
                    break;
            }
        }

        private void ResultDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            var row = (DataRowView) resultsGrid.SelectedItem;
            if (row == null) return;
            if (CardAdded != null)
                CardAdded(this, new SearchCardIdEventArgs {CardId = Guid.Parse(row["id"] as string)});
        }

        private void ResultCardSelected(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = true;
            var row = (DataRowView) resultsGrid.SelectedItem;

            if (CardSelected != null)
                CardSelected(this,
                             row != null
                                 ? new SearchCardImageEventArgs
                                       {SetId = Guid.Parse(row["set_id"] as string), Image = (string) row["image"]}
                                 : new SearchCardImageEventArgs());
        }

        private void GenerateColumns(Data.Game game)
        {
            foreach (PropertyDef prop in game.CustomProperties)
            {
                resultsGrid.Columns.Add(new DataGridTextColumn
                                            {
                                                Binding = new Binding
                                                              {
                                                                  Path = new PropertyPath(prop.Name),
                                                                  Mode = BindingMode.OneTime
                                                              },
                                                Header = prop.Name
                                            });
            }
        }

        private void AddFilter(object sender, RoutedEventArgs e)
        {
            filterList.Items.Add(((FrameworkElement) sender).DataContext);
        }

        private void RemoveFilter(object sender, EventArgs e)
        {
            int idx = filterList.ItemContainerGenerator.IndexFromContainer((DependencyObject) sender);
            filterList.Items.RemoveAt(idx);
        }

        private void RefreshSearch(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            var conditions = new string[filterList.Items.Count];
            ItemContainerGenerator generator = filterList.ItemContainerGenerator;
            for (int i = 0; i < filterList.Items.Count; i++)
            {
                DependencyObject container = generator.ContainerFromIndex(i);
                var filterCtrl = (FilterControl) VisualTreeHelper.GetChild(container, 0);
                conditions[i] = filterCtrl.GetSqlCondition();
            }
            SearchString.Text = ConvertToSQLString(conditions);
            //TODO Implement a way to take the text of SearchString and use it as the search parameters. 
            //It should be exactly the SQL Query, so no parsing *should* be needed (but be prepared for it to fail).
            resultsGrid.ItemsSource = Game.SelectCards(conditions).DefaultView;
        }
        private string ConvertToSQLString(string[] conditions)
        {
            var sb = new StringBuilder();
            sb.Append("SELECT * FROM Card");
            if (conditions != null)
            {
                string connector = " WHERE ";
                foreach (string condition in conditions)
                {
                    sb.Append(connector);
                    sb.Append("(");
                    sb.Append(condition);
                    sb.Append(")");
                    connector = " AND ";
                }
            }
            return sb.ToString();
        }
    }

    public class SearchCardIdEventArgs : EventArgs
    {
        public Guid CardId { get; set; }
    }

    public class SearchCardImageEventArgs : EventArgs
    {
        public Guid SetId { get; set; }
        public string Image { get; set; }
    }

    public class SetConverter : IMultiValueConverter
    {
        #region IMultiValueConverter Members

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2) return Binding.DoNothing;
            if (values[0] == DependencyProperty.UnsetValue ||
                values[1] == DependencyProperty.UnsetValue)
                return Binding.DoNothing;

            Guid setId = Guid.Parse(values[0] as string);
            var game = (Data.Game) values[1];
            Set set = game.GetSet(setId);
            return set != null ? set.Name : "(unknown)";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}