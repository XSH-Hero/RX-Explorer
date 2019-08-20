﻿using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Services.Store;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media.Animation;

namespace FileManager
{
    public sealed partial class MainPage : Page
    {
        private StoreContext Context;
        private IReadOnlyList<StorePackageUpdate> Updates;
        private List<string> SearchHistoryRecord;
        public static MainPage ThisPage { get; private set; }
        public bool IsNowSearching { get; set; }

        private Dictionary<Type, string> PageDictionary;

        public Dictionary<string, InitializePackage> AdminQuickStartCollection;

        public MainPage()
        {
            InitializeComponent();
            ThisPage = this;
            Window.Current.SetTitleBar(TitleBar);
            Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (ApplicationData.Current.LocalSettings.Values["SetSearchResultMaxNum"] == null)
            {
                ApplicationData.Current.LocalSettings.Values["SetSearchResultMaxNum"] = Convert.ToString(100);
            }

            PageDictionary = new Dictionary<Type, string>()
            {
                {typeof(WebTab), "浏览器"},
                {typeof(ThisPC),"这台电脑" },
                {typeof(FileControl),"这台电脑" },
                {typeof(AboutMe),"这台电脑" }
            };

            AdminQuickStartCollection = new Dictionary<string, InitializePackage>
            {
                {"应用商店" ,new InitializePackage("应用商店", Path.Combine(Package.Current.InstalledLocation.Path, "Assets\\MicrosoftStore.png"), "ms-windows-store://home", QuickStartType.Application) },
                {"计算器",new InitializePackage("计算器", Path.Combine(Package.Current.InstalledLocation.Path, "Assets\\Calculator.png"), "calculator:", QuickStartType.Application) },
                {"系统设置",new InitializePackage("系统设置", Path.Combine(Package.Current.InstalledLocation.Path, "Assets\\Setting.png"), "ms-settings:", QuickStartType.Application) },
                {"邮件",new InitializePackage("邮件", Path.Combine(Package.Current.InstalledLocation.Path, "Assets\\Email.png"), "mailto:", QuickStartType.Application) },
                {"日历",new InitializePackage("必应地图", Path.Combine(Package.Current.InstalledLocation.Path, "Assets\\Map.png"), "bingmaps:", QuickStartType.Application) },
                {"必应地图",new InitializePackage("必应地图", Path.Combine(Package.Current.InstalledLocation.Path, "Assets\\Map.png"), "bingmaps:", QuickStartType.Application) },
                {"天气",new InitializePackage("天气", Path.Combine(Package.Current.InstalledLocation.Path, "Assets\\Weather.png"), "bingweather:", QuickStartType.Application) },
                {"必应",new InitializePackage("必应", Path.Combine(Package.Current.InstalledLocation.Path, "Assets\\Bing.png"), "https://www.bing.com/", QuickStartType.WebSite) },
                {"百度",new InitializePackage("百度", Path.Combine(Package.Current.InstalledLocation.Path, "Assets\\Baidu.png"), "https://www.baidu.com/", QuickStartType.WebSite) },
                {"微信",new InitializePackage("微信", Path.Combine(Package.Current.InstalledLocation.Path, "Assets\\Wechat.png"), "https://wx.qq.com/", QuickStartType.WebSite) },
                {"IT之家",new InitializePackage("IT之家", Path.Combine(Package.Current.InstalledLocation.Path, "Assets\\iThome.jpg"), "https://www.ithome.com/", QuickStartType.WebSite) },
                {"微博",new InitializePackage("微博", Path.Combine(Package.Current.InstalledLocation.Path, "Assets\\Weibo.png"), "https://www.weibo.com/", QuickStartType.WebSite) }
            };

            SQLite SQL = SQLite.GetInstance();
            SearchHistoryRecord = await SQL.GetSearchHistoryAsync();

            if(ApplicationData.Current.LocalSettings.Values["DeletedAdminQuickStart"] is string Query)
            {
                var ToDeleteList = Query.Split(",", StringSplitOptions.RemoveEmptyEntries);
                foreach (var Item in AdminQuickStartCollection)
                {
                    if(ToDeleteList.Contains(Item.Key))
                    {
                        continue;
                    }
                    await SQL.SetQuickStartItemAsync(Item.Value.Name, Item.Value.FullPath, Item.Value.UriString, Item.Value.Type);
                }
            }
            else
            {
                foreach (var Item in AdminQuickStartCollection)
                {
                    await SQL.SetQuickStartItemAsync(Item.Value.Name, Item.Value.FullPath, Item.Value.UriString, Item.Value.Type);
                }
            }

            Nav.Navigate(typeof(ThisPC));

            await CheckAndInstallUpdate();

            await Task.Delay(30000);
            RequestRateApplication();
        }

        private void Nav_Navigated(object sender, Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            NavView.IsBackEnabled = Nav.CanGoBack;

            if (Nav.SourcePageType == typeof(SettingPage))
            {
                NavView.SelectedItem = NavView.SettingsItem as NavigationViewItem;
            }
            else
            {
                foreach (var MenuItem in from NavigationViewItemBase MenuItem in NavView.MenuItems
                                         where MenuItem is NavigationViewItem && MenuItem.Content.ToString() == PageDictionary[Nav.SourcePageType]
                                         select MenuItem)
                {
                    MenuItem.IsSelected = true;
                    break;
                }
            }
        }

        private bool WhetherUserRateAppInPast()
        {
            return ApplicationData.Current.LocalSettings.Values["IsRated"] is bool;
        }

        private void RequestRateApplication()
        {
            if (WhetherUserRateAppInPast())
            {
                return;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["IsRated"] = true;
            }

            RateTip.IsOpen = true;
            RateTip.ActionButtonClick += async (s, e) =>
            {
                var Result = await Context.RequestRateAndReviewAppAsync();
                switch (Result.Status)
                {
                    case StoreRateAndReviewStatus.Succeeded:
                        ShowRateSucceedNotification();
                        break;
                    case StoreRateAndReviewStatus.CanceledByUser:
                        break;
                    default:
                        ShowRateErrorNotification();
                        break;
                }
            };
        }

        private void ShowRateSucceedNotification()
        {
            var Content = new ToastContent()
            {
                Scenario = ToastScenario.Default,
                Launch = "Updating",
                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = "评价成功"
                            },

                            new AdaptiveText()
                            {
                                Text = "感谢您对此App的评价，帮助我们做得更好。"
                            }
                        }
                    }
                },
            };
            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
        }

        private void ShowRateErrorNotification()
        {
            var Content = new ToastContent()
            {
                Scenario = ToastScenario.Default,
                Launch = "Updating",
                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = "评价失败"
                            },

                            new AdaptiveText()
                            {
                                Text = "因网络或其他原因而无法进行评价"
                            }
                        }
                    }
                },
            };
            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
        }


        private async Task CheckAndInstallUpdate()
        {
            try
            {
                Context = StoreContext.GetDefault();
                Updates = await Context.GetAppAndOptionalStorePackageUpdatesAsync();

                if (Updates.Count > 0)
                {
                    UpdateTip.Subtitle = "最新版RX文件管理器已推出！\r最新版包含针对以往问题的修复补丁\r是否立即下载？";

                    UpdateTip.ActionButtonClick += async (s, e) =>
                    {
                        s.IsOpen = false;
                        SendUpdatableToastWithProgress();

                        if (Context.CanSilentlyDownloadStorePackageUpdates)
                        {
                            IProgress<StorePackageUpdateStatus> DownloadProgress = new Progress<StorePackageUpdateStatus>((Status) =>
                            {
                                if (Status.PackageDownloadProgress > 0.8)
                                {
                                    return;
                                }

                                string Tag = "RX-Updating";
                                var data = new NotificationData
                                {
                                    SequenceNumber = 0
                                };
                                data.Values["ProgressValue"] = (Status.PackageDownloadProgress * 1.25).ToString("0.##");
                                data.Values["ProgressString"] = Math.Ceiling(Status.PackageDownloadProgress * 125).ToString() + "%";

                                ToastNotificationManager.CreateToastNotifier().Update(data, Tag);
                            });

                            StorePackageUpdateResult DownloadResult = await Context.TrySilentDownloadAndInstallStorePackageUpdatesAsync(Updates).AsTask(DownloadProgress);

                            if (DownloadResult.OverallState == StorePackageUpdateState.Completed)
                            {
                                _ = await Context.TrySilentDownloadAndInstallStorePackageUpdatesAsync(Updates).AsTask();
                            }
                            else
                            {
                                ShowErrorNotification();
                            }
                        }
                        else
                        {
                            IProgress<StorePackageUpdateStatus> DownloadProgress = new Progress<StorePackageUpdateStatus>((Status) =>
                            {
                                if (Status.PackageDownloadProgress > 0.8)
                                {
                                    return;
                                }

                                string Tag = "RX-Updating";
                                var data = new NotificationData
                                {
                                    SequenceNumber = 0
                                };
                                data.Values["ProgressValue"] = (Status.PackageDownloadProgress * 1.25).ToString("0.##");
                                data.Values["ProgressString"] = Math.Ceiling(Status.PackageDownloadProgress * 125).ToString() + "%";

                                ToastNotificationManager.CreateToastNotifier().Update(data, Tag);
                            });

                            StorePackageUpdateResult DownloadResult = await Context.RequestDownloadAndInstallStorePackageUpdatesAsync(Updates).AsTask(DownloadProgress);

                            if (DownloadResult.OverallState == StorePackageUpdateState.Completed)
                            {
                                _ = await Context.RequestDownloadAndInstallStorePackageUpdatesAsync(Updates).AsTask();
                            }
                            else
                            {
                                ShowErrorNotification();
                            }
                        }
                    };

                    UpdateTip.IsOpen = true;
                }
            }
            catch (Exception)
            {
                ShowErrorNotification();
            }
        }

        private void ShowErrorNotification()
        {
            var Content = new ToastContent()
            {
                Scenario = ToastScenario.Default,
                Launch = "Updating",
                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = "更新失败"
                            },

                            new AdaptiveText()
                            {
                                Text = "RX文件管理器无法更新至最新版"
                            }
                        }
                    }
                },
            };
            ToastNotificationManager.History.Clear();
            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
        }

        private void SendUpdatableToastWithProgress()
        {
            string Tag = "RX-Updating";

            var content = new ToastContent()
            {
                Launch = "Updating",
                Scenario = ToastScenario.Reminder,
                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = "正在下载应用更新..."
                            },

                            new AdaptiveProgressBar()
                            {
                                Title = "正在更新",
                                Value = new BindableProgressBarValue("ProgressValue"),
                                Status = new BindableString("ProgressStatus"),
                                ValueStringOverride = new BindableString("ProgressString")
                            }
                        }
                    }
                }
            };

            var Toast = new ToastNotification(content.GetXml())
            {
                Tag = Tag,
                Data = new NotificationData()
            };
            Toast.Data.Values["ProgressValue"] = "0";
            Toast.Data.Values["ProgressStatus"] = "正在下载...";
            Toast.Data.Values["ProgressString"] = "0%";
            Toast.Data.SequenceNumber = 0;

            ToastNotificationManager.History.Clear();
            ToastNotificationManager.CreateToastNotifier().Show(Toast);
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                Nav.Navigate(typeof(SettingPage), null, new DrillInNavigationTransitionInfo());
            }
            else
            {
                switch (args.InvokedItem.ToString())
                {
                    case "这台电脑":
                        Nav.Navigate(typeof(ThisPC), null, new DrillInNavigationTransitionInfo()); break;
                    case "浏览器":
                        Nav.Navigate(typeof(WebTab), null, new DrillInNavigationTransitionInfo()); break;
                }
            }
        }

        private void Nav_Navigating(object sender, Windows.UI.Xaml.Navigation.NavigatingCancelEventArgs e)
        {
            if (Nav.CurrentSourcePageType == e.SourcePageType)
            {
                e.Cancel = true;
            }
        }

        private async void GlobeSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(args.QueryText))
            {
                return;
            }

            FlyoutBase.ShowAttachedFlyout(sender);

            if (!SearchHistoryRecord.Contains(args.QueryText))
            {
                SearchHistoryRecord.Add(args.QueryText);
                await SQLite.GetInstance().SetSearchHistoryAsync(args.QueryText);
            }
        }

        private void GlobeSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(sender.Text))
            {
                if (IsNowSearching)
                {
                    FileControl.ThisPage.Nav.GoBack();
                }
                return;
            }
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                List<string> FilterResult = SearchHistoryRecord.FindAll((s) => s.Contains(sender.Text, StringComparison.OrdinalIgnoreCase));
                if (FilterResult.Count == 0)
                {
                    FilterResult.Add("无建议");
                }
                sender.ItemsSource = FilterResult;
            }
        }

        private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            switch (Nav.CurrentSourcePageType.Name)
            {
                case "FileControl":
                    if (FileControl.ThisPage.Nav.CanGoBack)
                    {
                        FileControl.ThisPage.Nav.GoBack();
                    }
                    else if (Nav.CanGoBack)
                    {
                        Nav.GoBack();
                    }
                    break;
                default:
                    if (Nav.CanGoBack)
                    {
                        Nav.GoBack();
                    }
                    break;
            }
        }

        private void SearchConfirm_Click(object sender, RoutedEventArgs e)
        {
            SearchFlyout.Hide();

            if (ApplicationData.Current.LocalSettings.Values["LaunchSearchTips"] == null)
            {
                ApplicationData.Current.LocalSettings.Values["LaunchSearchTips"] = true;
                SearchTip.IsOpen = true;
            }

            IsNowSearching = true;

            QueryOptions Options;
            if ((bool)ShallowRadio.IsChecked)
            {
                Options = new QueryOptions(CommonFileQuery.OrderByName, null)
                {
                    FolderDepth = FolderDepth.Shallow,
                    IndexerOption = IndexerOption.UseIndexerWhenAvailable,
                    ApplicationSearchFilter = "System.FileName:*" + GlobeSearch.Text + "*"
                };
            }
            else
            {
                Options = new QueryOptions(CommonFileQuery.OrderByName, null)
                {
                    FolderDepth = FolderDepth.Deep,
                    IndexerOption = IndexerOption.UseIndexerWhenAvailable,
                    ApplicationSearchFilter = "System.FileName:*" + GlobeSearch.Text + "*"
                };
            }

            Options.SetThumbnailPrefetch(ThumbnailMode.ListView, 60, ThumbnailOptions.ResizeThumbnail);

            if (FileControl.ThisPage.Nav.CurrentSourcePageType.Name != "SearchPage")
            {
                StorageItemQueryResult FileQuery = (FileControl.ThisPage.FolderTree.RootNodes.FirstOrDefault().Content as StorageFolder).CreateItemQueryWithOptions(Options);

                FileControl.ThisPage.Nav.Navigate(typeof(SearchPage), FileQuery, new DrillInNavigationTransitionInfo());
            }
            else
            {
                SearchPage.ThisPage.SetSearchTarget = Options;
            }
        }

        private void SearchCancel_Click(object sender, RoutedEventArgs e)
        {
            SearchFlyout.Hide();
        }
    }
}