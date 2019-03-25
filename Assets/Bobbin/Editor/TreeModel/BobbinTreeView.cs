using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;

namespace Bobbin
{
    internal class BobbinTreeView : TreeViewWithTreeModel<BobbinPath>
    {
        const float kRowHeights = 20f;
        const float kToggleWidth = 18f;

        // All columns
        enum MyColumns
        {
            Toggle,
            Name,
            Value1,
            Value2,
            Value3,
        }

        public enum SortOption
        {
            Toggle,
            Name,
            Value1,
            Value2,
            Value3,
        }

        // Sort options per column
        SortOption[] m_SortOptions =
        {
            SortOption.Toggle,
            SortOption.Name,
            SortOption.Value1,
            SortOption.Value2,
            SortOption.Value3,
        };

        public static void TreeToList(TreeViewItem root, IList<TreeViewItem> result)
        {
            if (root == null)
                throw new NullReferenceException("root");
            if (result == null)
                throw new NullReferenceException("result");

            result.Clear();

            if (root.children == null)
                return;

            Stack<TreeViewItem> stack = new Stack<TreeViewItem>();
            for (int i = root.children.Count - 1; i >= 0; i--)
                stack.Push(root.children[i]);

            while (stack.Count > 0)
            {
                TreeViewItem current = stack.Pop();
                result.Add(current);

                if (current.hasChildren && current.children[0] != null)
                {
                    for (int i = current.children.Count - 1; i >= 0; i--)
                    {
                        stack.Push(current.children[i]);
                    }
                }
            }
        }

        public BobbinTreeView(TreeViewState state, MultiColumnHeader multicolumnHeader, TreeModel<BobbinPath> model) : base(state, multicolumnHeader, model)
        {
            Assert.AreEqual(m_SortOptions.Length, Enum.GetValues(typeof(MyColumns)).Length, "Ensure number of sort options are in sync with number of MyColumns enum values");

            // Custom setup
            rowHeight = kRowHeights;
            columnIndexForTreeFoldouts = 2;
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            customFoldoutYOffset = (kRowHeights - EditorGUIUtility.singleLineHeight) * 0.5f; // center foldout in the row since we also center content. See RowGUI
            extraSpaceBeforeIconAndLabel = kToggleWidth;
            multicolumnHeader.sortingChanged += OnSortingChanged;

            Reload();
        }


        // Note we We only build the visible rows, only the backend has the full tree information. 
        // The treeview only creates info for the row list.
        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            var rows = base.BuildRows(root);
            SortIfNeeded(root, rows);
            return rows;
        }

        void OnSortingChanged(MultiColumnHeader multiColumnHeader)
        {
            SortIfNeeded(rootItem, GetRows());
        }

        void SortIfNeeded(TreeViewItem root, IList<TreeViewItem> rows)
        {
            if (rows.Count <= 1)
                return;

            if (multiColumnHeader.sortedColumnIndex == -1)
            {
                return; // No column to sort for (just use the order the data are in)
            }

            // Sort the roots of the existing tree items
            SortByMultipleColumns();
            TreeToList(root, rows);
            Repaint();
        }

        void SortByMultipleColumns()
        {
            var sortedColumns = multiColumnHeader.state.sortedColumns;

            if (sortedColumns.Length == 0)
                return;

            var myTypes = rootItem.children.Cast<TreeViewItem<BobbinPath>>();
            var orderedQuery = InitialOrder(myTypes, sortedColumns);
            for (int i = 1; i < sortedColumns.Length; i++)
            {
                SortOption sortOption = m_SortOptions[sortedColumns[i]];
                bool ascending = multiColumnHeader.IsSortedAscending(sortedColumns[i]);

                switch (sortOption)
                {
                    case SortOption.Toggle:
                        orderedQuery = orderedQuery.ThenBy(l => l.data.enabled, ascending);
                        break;
                    case SortOption.Name:
                        orderedQuery = orderedQuery.ThenBy(l => l.data.name, ascending);
                        break;
                    case SortOption.Value1:
                        orderedQuery = orderedQuery.ThenBy(l => l.data.url, ascending);
                        break;
                    case SortOption.Value2:
                        orderedQuery = orderedQuery.ThenBy(l => l.data.fileType, ascending);
                        break;
                    case SortOption.Value3:
                        orderedQuery = orderedQuery.ThenBy(l => l.data.assetReference.name, ascending);
                        break;
                }
            }

            rootItem.children = orderedQuery.Cast<TreeViewItem>().ToList();
        }

        IOrderedEnumerable<TreeViewItem<BobbinPath>> InitialOrder(IEnumerable<TreeViewItem<BobbinPath>> myTypes, int[] history)
        {
            SortOption sortOption = m_SortOptions[history[0]];
            bool ascending = multiColumnHeader.IsSortedAscending(history[0]);
            switch (sortOption)
            {
                case SortOption.Toggle:
                    return myTypes.Order(l => l.data.enabled, ascending);
                case SortOption.Name:
                    return myTypes.Order(l => l.data.name, ascending);
                case SortOption.Value1:
                    return myTypes.Order(l => l.data.url, ascending);
                case SortOption.Value2:
                    return myTypes.Order(l => l.data.fileType, ascending);
                case SortOption.Value3:
                    return myTypes.Order(l => l.data.assetReference.name, ascending);
                default:
                    Assert.IsTrue(false, "Unhandled enum");
                    break;
            }

            // default
            return myTypes.Order(l => l.data.name, ascending);
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var item = (TreeViewItem<BobbinPath>)args.item;

            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
            {
                CellGUI(args.GetCellRect(i), item, (MyColumns)args.GetColumn(i), ref args);
            }
        }

        void CellGUI(Rect cellRect, TreeViewItem<BobbinPath> item, MyColumns column, ref RowGUIArgs args)
        {
            // Center cell rect vertically (makes it easier to place controls, icons etc in the cells)
            CenterRectUsingSingleLineHeight(ref cellRect);

            switch (column)
            {
                // case MyColumns.Icon1:
                // 	{
                // 		GUI.DrawTexture(cellRect, s_TestIcons[GetIcon1Index(item)], ScaleMode.ScaleToFit);
                // 	}
                // 	break;
                case MyColumns.Toggle:
                    {
                        item.data.enabled = EditorGUI.Toggle(cellRect, new GUIContent("", "enable refresh?"), item.data.enabled); // hide when outside cell rect
                    }
                    break;

                case MyColumns.Name:
                    {
                        cellRect.width -= 20;
                        item.data.name = GUI.TextField(cellRect, item.data.name);
                        cellRect.x += cellRect.width;
                        cellRect.width = 20;
                        GUI.backgroundColor = Color.Lerp(Color.red, Color.white, 0.75f);
                        if (GUI.Button(cellRect, new GUIContent("x", "delete this item")))
                        {
                            if (EditorUtility.DisplayDialog("Bobbin: confirm deletion", "Really delete " + item.data.name + "?", "Yes, delete", "No, cancel"))
                            {
                                var list = new List<BobbinPath>();
                                list.Add(item.data);
                                treeModel.RemoveElements(list);
                            }
                        }
                        GUI.backgroundColor = Color.white;
                    }
                    break;

                case MyColumns.Value1:
                case MyColumns.Value2:
                case MyColumns.Value3:
                    {

                        cellRect.xMin += 5f; // When showing controls make some extra spacing

                        if (column == MyColumns.Value1)
                        {
                            bool hasURL = item.data.url != null && item.data.url.Length > 4;
                            if ( hasURL ) {
                                cellRect.width -= 20;
                            }
                            item.data.url = GUI.TextField(cellRect, item.data.url);
                            if ( hasURL ) {
                                cellRect.x += cellRect.width;
                                cellRect.width = 20;
                                if ( GUI.Button( cellRect, new GUIContent(">", "click to view in web browser: " + BobbinCore.UnfixURL(item.data.url) ) ) ) {
                                    Application.OpenURL( BobbinCore.UnfixURL(item.data.url) );
                                }
                            }
                            
                        }
                        if (column == MyColumns.Value2)
                        {
                            item.data.fileType = (Bobbin.FileType)EditorGUI.EnumPopup(cellRect, (Enum)item.data.fileType);
                        }
                        if (column == MyColumns.Value3)
                        {
                            if (item.data.assetReference != null)
                            {
                                cellRect.x += 24;
                                cellRect.width -= 24;
                                GUI.enabled = false;
                                item.data.assetReference = EditorGUI.ObjectField(cellRect, item.data.assetReference, typeof(UnityEngine.Object), false);
                                GUI.enabled = true;
                                cellRect.x -= 20;
                                cellRect.width = 20;
                                if (GUI.Button(cellRect, new GUIContent("x", "reset asset file path\n" + item.data.filePath)))
                                {
                                    item.data.assetReference = null;
                                    item.data.filePath = "";
                                    item.data.lastFileHash = "";
                                }
                            }
                            else
                            {
                                if (GUI.Button(cellRect, new GUIContent("Save As...", "click to select asset file path")))
                                {
                                    var newPath = EditorUtility.SaveFilePanelInProject("Bobbin: save " + item.data.name + " URL as file...", item.data.name + "." + item.data.fileType.ToString(), item.data.fileType.ToString(), "Save URL as file...");
                                    if (newPath != null && newPath.Length > 0)
                                    {
                                        item.data.filePath = newPath;
                                        item.data.lastFileHash = "";
                                        if (item.data.url.Length > 4)
                                        { // only fetch from WWW if user inputed a URL
                                            BobbinCore.DoRefresh();
                                        }
                                    }
                                } // end if button
                            } // end else
                        } // end if column3

                    }

                    break;
            }
        }

        // Rename
        //--------

        protected override bool CanRename(TreeViewItem item)
        {
            // Only allow rename if we can show the rename overlay with a certain width (label might be clipped by other columns)
            //Rect renameRect = GetRenameRect (treeViewRect, 0, item);
            //return renameRect.width > 30;
            return false;
        }

        protected override void RenameEnded(RenameEndedArgs args)
        {
            // Set the backend name and reload the tree to reflect the new model
            if (args.acceptedRename)
            {
                var element = treeModel.Find(args.itemID);
                element.name = args.newName;
                Reload();
            }
        }

        protected override Rect GetRenameRect(Rect rowRect, int row, TreeViewItem item)
        {
            Rect cellRect = GetCellRectForTreeFoldouts(rowRect);
            CenterRectUsingSingleLineHeight(ref cellRect);
            return base.GetRenameRect(cellRect, row, item);
        }

        protected override bool CanStartDrag(CanStartDragArgs args)
        {
            return false;
        }

        // Misc
        //--------

        protected override bool CanMultiSelect(TreeViewItem item)
        {
            return true;
        }

        public static MultiColumnHeaderState CreateDefaultMultiColumnHeaderState(float treeViewWidth)
        {
            var columns = new[]
            {
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("▶", "Enable or disable refreshing for each file"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Center,
                    width = 24,
                    minWidth = 24,
                    autoResize = false,
                    allowToggleVisibility = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Name", "Write a descriptive note or label here (like 'Dialogue1' or 'FinalStats' or 'Level3')"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Right,
                    width = 110,
                    minWidth = 60,
                    autoResize = true,
                    allowToggleVisibility = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("URL", "Bobbin will try to fetch and download content at this URL."),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Right,
                    width = 110,
                    minWidth = 60,
                    autoResize = true,
                    allowToggleVisibility = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Type", "if you need more file extensions, edit the FileType enum in BobbinSettings.cs"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Right,
                    width = 48,
                    minWidth = 40,
                    autoResize = true,
                    allowToggleVisibility = true
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Asset", "The generated asset file as imported by Unity."),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Right,
                    width = 70,
                    minWidth = 40,
                    autoResize = true,
                    allowToggleVisibility = true
                }
            };

            Assert.AreEqual(columns.Length, Enum.GetValues(typeof(MyColumns)).Length, "Number of columns should match number of enum values: You probably forgot to update one of them.");

            var state = new MultiColumnHeaderState(columns);
            return state;
        }
    }

    static class MyExtensionMethods
    {
        public static IOrderedEnumerable<T> Order<T, TKey>(this IEnumerable<T> source, Func<T, TKey> selector, bool ascending)
        {
            if (ascending)
            {
                return source.OrderBy(selector);
            }
            else
            {
                return source.OrderByDescending(selector);
            }
        }

        public static IOrderedEnumerable<T> ThenBy<T, TKey>(this IOrderedEnumerable<T> source, Func<T, TKey> selector, bool ascending)
        {
            if (ascending)
            {
                return source.ThenBy(selector);
            }
            else
            {
                return source.ThenByDescending(selector);
            }
        }
    }
}
