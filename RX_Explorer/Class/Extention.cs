﻿using Force.Crc32;
using Google.Cloud.Translation.V2;
using Microsoft.Toolkit.Uwp.Notifications;
using NetworkAccess;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using TreeView = Microsoft.UI.Xaml.Controls.TreeView;
using TreeViewItem = Microsoft.UI.Xaml.Controls.TreeViewItem;
using TreeViewNode = Microsoft.UI.Xaml.Controls.TreeViewNode;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供扩展方法的静态类
    /// </summary>
    public static class Extention
    {
        public static IEnumerable<T> OrderByLikeFileSystem<T>(this IEnumerable<T> Input, Func<T, string> GetString, SortDirection Direction)
        {
            if (Input.Any())
            {
                int MaxLength = Input.Select(Item => GetString(Item).Length).Max();

                if (Direction == SortDirection.Ascending)
                {
                    return Input.Select(Item => new
                    {
                        OriginItem = Item,
                        SortString = Regex.Replace(GetString(Item), @"(\d+)|(\D+)", Eva => Eva.Value.PadLeft(MaxLength, char.IsDigit(Eva.Value[0]) ? ' ' : '\xffff'))
                    }).OrderBy(x => x.SortString).Select(x => x.OriginItem);
                }
                else
                {
                    return Input.Select(Item => new
                    {
                        OriginItem = Item,
                        SortString = Regex.Replace(GetString(Item), @"(\d+)|(\D+)", Eva => Eva.Value.PadLeft(MaxLength, char.IsDigit(Eva.Value[0]) ? ' ' : '\xffff'))
                    }).OrderByDescending(x => x.SortString).Select(x => x.OriginItem);
                }
            }
            else
            {
                return new List<T>();
            }
        }

        /// <summary>
        /// 将16进制字符串转换成Color对象
        /// </summary>
        /// <param name="Hex">十六进制字符串</param>
        /// <returns></returns>
        public static Color GetColorFromHexString(this string Hex)
        {
            if (string.IsNullOrWhiteSpace(Hex))
            {
                throw new ArgumentException("Hex could not be null or empty", nameof(Hex));
            }

            Hex = Hex.Replace("#", string.Empty);

            bool ExistAlpha = Hex.Length == 8 || Hex.Length == 4;
            bool IsDoubleHex = Hex.Length == 8 || Hex.Length == 6;

            if (!ExistAlpha && Hex.Length != 6 && Hex.Length != 3)
            {
                throw new ArgumentException("Hex is invalid");
            }

            int n = 0;
            byte a;
            int HexCount = IsDoubleHex ? 2 : 1;
            if (ExistAlpha)
            {
                n = HexCount;
                a = (byte)Convert.ToUInt32(Hex.Substring(0, HexCount), 16);
                if (!IsDoubleHex)
                {
                    a = (byte)(a * 16 + a);
                }
            }
            else
            {
                a = 0xFF;
            }

            var r = (byte)Convert.ToUInt32(Hex.Substring(n, HexCount), 16);
            var g = (byte)Convert.ToUInt32(Hex.Substring(n + HexCount, HexCount), 16);
            var b = (byte)Convert.ToUInt32(Hex.Substring(n + (2 * HexCount), HexCount), 16);
            if (!IsDoubleHex)
            {
                r = (byte)(r * 16 + r);
                g = (byte)(g * 16 + g);
                b = (byte)(b * 16 + b);
            }

            return Color.FromArgb(a, r, g, b);
        }

        public static async Task MoveSubFilesAndSubFoldersAsync(this StorageFolder Folder, StorageFolder TargetFolder)
        {
            if (Folder == null)
            {
                throw new ArgumentNullException(nameof(Folder), "Parameter could not be null");
            }

            if (TargetFolder == null)
            {
                throw new ArgumentNullException(nameof(TargetFolder), "Parameter could not be null");
            }

            foreach (IStorageItem Item in await Folder.GetItemsAsync())
            {
                if (Item is StorageFolder SubFolder)
                {
                    StorageFolder NewFolder = await TargetFolder.CreateFolderAsync(SubFolder.Name, CreationCollisionOption.OpenIfExists);

                    await MoveSubFilesAndSubFoldersAsync(SubFolder, NewFolder).ConfigureAwait(false);
                }
                else
                {
                    await ((StorageFile)Item).MoveAsync(TargetFolder, Item.Name, NameCollisionOption.GenerateUniqueName);
                }
            }

            await Folder.DeleteAllSubFilesAndFolders().ConfigureAwait(false);
        }

        public static async Task CopySubFilesAndSubFoldersAsync(this StorageFolder Folder, StorageFolder TargetFolder)
        {
            if (Folder == null)
            {
                throw new ArgumentNullException(nameof(Folder), "Parameter could not be null");
            }

            if (TargetFolder == null)
            {
                throw new ArgumentNullException(nameof(TargetFolder), "Parameter could not be null");
            }

            foreach (var Item in await Folder.GetItemsAsync())
            {
                if (Item is StorageFolder SubFolder)
                {
                    StorageFolder NewFolder = await TargetFolder.CreateFolderAsync(SubFolder.Name, CreationCollisionOption.OpenIfExists);
                    await CopySubFilesAndSubFoldersAsync(SubFolder, NewFolder).ConfigureAwait(false);
                }
                else
                {
                    await ((StorageFile)Item).CopyAsync(TargetFolder, Item.Name, NameCollisionOption.GenerateUniqueName);
                }
            }
        }

        public static bool CanTraceToRootNode(this TreeViewNode Node, TreeViewNode RootNode)
        {
            if (Node == null)
            {
                throw new ArgumentNullException(nameof(Node), "Argument could not be null");
            }

            if (RootNode == null)
            {
                return false;
            }

            if (Node == RootNode)
            {
                return true;
            }
            else
            {
                if (Node.Parent != null && Node.Depth != 0)
                {
                    return Node.Parent.CanTraceToRootNode(RootNode);
                }
                else
                {
                    Debug.WriteLine($"Could not found the root node, return false");
                    return false;
                }
            }
        }

        public static async Task UpdateAllSubNodeAsync(this TreeViewNode Node)
        {
            if (Node == null)
            {
                throw new ArgumentNullException(nameof(Node), "Node could not be null");
            }

            if (Node.Children.Count > 0)
            {
                List<string> FolderList = WIN_Native_API.GetStorageItemsPath((Node.Content as TreeViewNodeContent).Path, SettingControl.IsDisplayHiddenItem, ItemFilters.Folder);
                List<string> PathList = Node.Children.Select((Item) => (Item.Content as TreeViewNodeContent).Path).ToList();
                List<string> AddList = FolderList.Except(PathList).ToList();
                List<string> RemoveList = PathList.Except(FolderList).ToList();

                foreach (string AddPath in AddList)
                {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                    {
                        Node.Children.Add(new TreeViewNode
                        {
                            Content = new TreeViewNodeContent(AddPath),
                            HasUnrealizedChildren = WIN_Native_API.CheckContainsAnyItem(AddPath, ItemFilters.Folder),
                            IsExpanded = false
                        });
                    });
                }

                foreach (string RemovePath in RemoveList)
                {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                    {
                        if (Node.Children.FirstOrDefault((Item) => (Item.Content as TreeViewNodeContent)?.Path == RemovePath) is TreeViewNode RemoveNode)
                        {
                            Node.Children.Remove(RemoveNode);
                        }
                    });
                }

                foreach (TreeViewNode SubNode in Node.Children)
                {
                    await SubNode.UpdateAllSubNodeAsync().ConfigureAwait(true);
                }
            }
            else
            {
                Node.HasUnrealizedChildren = WIN_Native_API.CheckContainsAnyItem((Node.Content as TreeViewNodeContent).Path, ItemFilters.Folder);
            }
        }

        public static async Task<TreeViewNode> GetChildNodeAsync(this TreeViewNode Node, PathAnalysis Analysis, bool DoNotExpandNodeWhenSearching = false)
        {
            if (Node.HasUnrealizedChildren && !Node.IsExpanded && !DoNotExpandNodeWhenSearching)
            {
                Node.IsExpanded = true;
            }

            string NextPathLevel = Analysis.NextFullPath();

            if (NextPathLevel == Analysis.FullPath)
            {
                if ((Node.Content as TreeViewNodeContent).Path == NextPathLevel)
                {
                    return Node;
                }
                else
                {
                    if (DoNotExpandNodeWhenSearching)
                    {
                        if (Node.Children.FirstOrDefault((SubNode) => (SubNode.Content as TreeViewNodeContent).Path == NextPathLevel) is TreeViewNode TargetNode)
                        {
                            return TargetNode;
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        while (true)
                        {
                            if (Node.Children.FirstOrDefault((SubNode) => (SubNode.Content as TreeViewNodeContent).Path == NextPathLevel) is TreeViewNode TargetNode)
                            {
                                return TargetNode;
                            }
                            else
                            {
                                await Task.Delay(200).ConfigureAwait(true);
                            }
                        }
                    }
                }
            }
            else
            {
                if ((Node.Content as TreeViewNodeContent).Path == NextPathLevel)
                {
                    return await GetChildNodeAsync(Node, Analysis, DoNotExpandNodeWhenSearching).ConfigureAwait(true);
                }
                else
                {
                    if (DoNotExpandNodeWhenSearching)
                    {
                        if (Node.Children.FirstOrDefault((SubNode) => (SubNode.Content as TreeViewNodeContent).Path == NextPathLevel) is TreeViewNode TargetNode)
                        {
                            return await GetChildNodeAsync(TargetNode, Analysis, DoNotExpandNodeWhenSearching).ConfigureAwait(true);
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        while (true)
                        {
                            if (Node.Children.FirstOrDefault((SubNode) => (SubNode.Content as TreeViewNodeContent).Path == NextPathLevel) is TreeViewNode TargetNode)
                            {
                                return await GetChildNodeAsync(TargetNode, Analysis, DoNotExpandNodeWhenSearching).ConfigureAwait(true);
                            }
                            else
                            {
                                await Task.Delay(200).ConfigureAwait(true);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 使用GoogleAPI自动检测语言并将文字翻译为对应语言
        /// </summary>
        /// <param name="Text">要翻译的内容</param>
        /// <returns></returns>
        public static Task<string> TranslateAsync(this string Text)
        {
            return Task.Run(() =>
            {
                using (SecureString Secure = SecureAccessProvider.GetGoogleTranslateAccessKey(Package.Current))
                {
                    IntPtr Bstr = Marshal.SecureStringToBSTR(Secure);
                    string APIKey = Marshal.PtrToStringBSTR(Bstr);

                    try
                    {
                        using (TranslationClient Client = TranslationClient.CreateFromApiKey(APIKey, TranslationModel.ServiceDefault))
                        {
                            Detection DetectResult = Client.DetectLanguage(Text);

                            string CurrentLanguage = string.Empty;

                            switch (Globalization.CurrentLanguage)
                            {
                                case LanguageEnum.English:
                                    {
                                        CurrentLanguage = LanguageCodes.English;
                                        break;
                                    }

                                case LanguageEnum.Chinese_Simplified:
                                    {
                                        CurrentLanguage = LanguageCodes.ChineseSimplified;
                                        break;
                                    }
                                case LanguageEnum.Chinese_Traditional:
                                    {
                                        CurrentLanguage = LanguageCodes.ChineseTraditional;
                                        break;
                                    }
                                case LanguageEnum.French:
                                    {
                                        CurrentLanguage = LanguageCodes.French;
                                        break;
                                    }
                            }

                            if (DetectResult.Language.StartsWith(CurrentLanguage))
                            {
                                return Text;
                            }
                            else
                            {
                                TranslationResult TranslateResult = Client.TranslateText(Text, CurrentLanguage, DetectResult.Language);
                                return TranslateResult.TranslatedText;
                            }
                        }
                    }
                    catch
                    {
                        return Text;
                    }
                    finally
                    {
                        Marshal.ZeroFreeBSTR(Bstr);
                        unsafe
                        {
                            fixed (char* ClearPtr = APIKey)
                            {
                                for (int i = 0; i < APIKey.Length; i++)
                                {
                                    ClearPtr[i] = '\0';
                                }
                            }
                        }
                    }
                }
            });
        }

        /// <summary>
        /// 选中TreeViewNode并将其滚动到UI中间
        /// </summary>
        /// <param name="Node">要选中的Node</param>
        /// <param name="View">Node所属的TreeView控件</param>
        /// <returns></returns>
        public static void SelectNode(this TreeView View, TreeViewNode Node)
        {
            if (View == null)
            {
                throw new ArgumentNullException(nameof(View), "Parameter could not be null");
            }

            View.SelectedNode = null;
            View.SelectedNode = Node;

            View.UpdateLayout();

            if (View.ContainerFromNode(Node) is TreeViewItem Item)
            {
                Item.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true, VerticalAlignmentRatio = 0.5 });
            }
        }

        /// <summary>
        /// 检查文件是否存在于物理驱动器上
        /// </summary>
        /// <param name="Item">存储对象</param>
        /// <returns></returns>
        public static async Task<bool> CheckExist(this IStorageItem Item)
        {
            if (Item == null)
            {
                throw new ArgumentNullException(nameof(Item), "Parameter could not be null");
            }

            if (Item is StorageFile File)
            {
                try
                {
                    if ((await File.GetParentAsync()) is StorageFolder ParentFolder)
                    {
                        return (await ParentFolder.TryGetItemAsync(File.Name)) != null;
                    }
                    else
                    {
                        try
                        {
                            _ = await StorageFile.GetFileFromPathAsync(File.Path);
                            return true;
                        }
                        catch (Exception)
                        {
                            return false;
                        }
                    }
                }
                catch (Exception)
                {
                    try
                    {
                        _ = await StorageFile.GetFileFromPathAsync(File.Path);
                        return true;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
            }
            else if (Item is StorageFolder Folder)
            {
                try
                {
                    if ((await Folder.GetParentAsync()) is StorageFolder ParenetFolder)
                    {
                        return (await ParenetFolder.TryGetItemAsync(Folder.Name)) != null;
                    }
                    else
                    {
                        try
                        {
                            _ = await StorageFolder.GetFolderFromPathAsync(Folder.Path);
                            return true;
                        }
                        catch (Exception)
                        {
                            return false;
                        }
                    }
                }
                catch (Exception)
                {
                    try
                    {
                        _ = await StorageFolder.GetFolderFromPathAsync(Folder.Path);
                        return true;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 根据指定的密钥使用AES-128-CBC加密字符串
        /// </summary>
        /// <param name="OriginText">要加密的内容</param>
        /// <param name="Key">密钥</param>
        /// <returns></returns>
        public static async Task<string> EncryptAsync(this string OriginText, string Key)
        {
            if (string.IsNullOrEmpty(OriginText))
            {
                throw new ArgumentNullException(nameof(OriginText), "Parameter could not be null or empty");
            }

            if (string.IsNullOrEmpty(Key))
            {
                throw new ArgumentNullException(nameof(Key), "Parameter could not be null or empty");
            }

            try
            {
                using (SecureString Secure = SecureAccessProvider.GetStringEncryptionAesIV(Package.Current))
                {
                    IntPtr Bstr = Marshal.SecureStringToBSTR(Secure);
                    string IV = Marshal.PtrToStringBSTR(Bstr);

                    try
                    {
                        using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
                        {
                            KeySize = 128,
                            Key = Key.Length > 16 ? Encoding.UTF8.GetBytes(Key.Substring(0, 16)) : Encoding.UTF8.GetBytes(Key.PadRight(16, '0')),
                            Mode = CipherMode.CBC,
                            Padding = PaddingMode.PKCS7,
                            IV = Encoding.UTF8.GetBytes(IV)
                        })
                        {
                            using (MemoryStream EncryptStream = new MemoryStream())
                            {
                                using (ICryptoTransform Encryptor = AES.CreateEncryptor())
                                using (CryptoStream TransformStream = new CryptoStream(EncryptStream, Encryptor, CryptoStreamMode.Write))
                                {
                                    using (StreamWriter Writer = new StreamWriter(TransformStream))
                                    {
                                        await Writer.WriteAsync(OriginText).ConfigureAwait(false);
                                    }
                                }

                                return Convert.ToBase64String(EncryptStream.ToArray());
                            }
                        }
                    }
                    finally
                    {
                        Marshal.ZeroFreeBSTR(Bstr);
                        unsafe
                        {
                            fixed (char* ClearPtr = IV)
                            {
                                for (int i = 0; i < IV.Length; i++)
                                {
                                    ClearPtr[i] = '\0';
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 根据指定的密钥解密密文
        /// </summary>
        /// <param name="OriginText">密文</param>
        /// <param name="Key">密钥</param>
        /// <returns></returns>
        public static async Task<string> DecryptAsync(this string OriginText, string Key)
        {
            if (string.IsNullOrEmpty(OriginText))
            {
                throw new ArgumentNullException(nameof(OriginText), "Parameter could not be null or empty");
            }

            if (string.IsNullOrEmpty(Key))
            {
                throw new ArgumentNullException(nameof(Key), "Parameter could not be null or empty");
            }

            try
            {
                using (SecureString Secure = SecureAccessProvider.GetStringEncryptionAesIV(Package.Current))
                {
                    IntPtr Bstr = Marshal.SecureStringToBSTR(Secure);
                    string IV = Marshal.PtrToStringBSTR(Bstr);

                    try
                    {
                        using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
                        {
                            KeySize = 128,
                            Key = Key.Length > 16 ? Encoding.UTF8.GetBytes(Key.Substring(0, 16)) : Encoding.UTF8.GetBytes(Key.PadRight(16, '0')),
                            Mode = CipherMode.CBC,
                            Padding = PaddingMode.PKCS7,
                            IV = Encoding.UTF8.GetBytes(IV)
                        })
                        {
                            using (MemoryStream DecryptStream = new MemoryStream(Convert.FromBase64String(OriginText)))
                            {
                                using (ICryptoTransform Decryptor = AES.CreateDecryptor())
                                using (CryptoStream TransformStream = new CryptoStream(DecryptStream, Decryptor, CryptoStreamMode.Read))
                                using (StreamReader Writer = new StreamReader(TransformStream, Encoding.UTF8))
                                {
                                    return await Writer.ReadToEndAsync().ConfigureAwait(false);
                                }
                            }
                        }
                    }
                    finally
                    {
                        Marshal.ZeroFreeBSTR(Bstr);
                        unsafe
                        {
                            fixed (char* ClearPtr = IV)
                            {
                                for (int i = 0; i < IV.Length; i++)
                                {
                                    ClearPtr[i] = '\0';
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 根据指定的密钥、加密强度，使用AES将文件加密并保存至指定的文件夹
        /// </summary>
        /// <param name="OriginFile">要加密的文件</param>
        /// <param name="ExportFolder">指定加密文件保存的文件夹</param>
        /// <param name="Key">加密密钥</param>
        /// <param name="KeySize">加密强度，值仅允许 128 和 256</param>
        /// <param name="CancelToken">取消通知</param>
        /// <returns></returns>
        public static async Task<StorageFile> EncryptAsync(this StorageFile OriginFile, StorageFolder ExportFolder, string Key, int KeySize, CancellationToken CancelToken = default)
        {
            if (OriginFile == null)
            {
                throw new ArgumentNullException(nameof(OriginFile), "OriginFile could not be null");
            }

            if (ExportFolder == null)
            {
                throw new ArgumentNullException(nameof(ExportFolder), "ExportFolder could not be null");
            }

            if (KeySize != 256 && KeySize != 128)
            {
                throw new InvalidEnumArgumentException("AES密钥长度仅支持128或256任意一种");
            }

            if (string.IsNullOrEmpty(Key))
            {
                throw new ArgumentNullException(nameof(Key), "Parameter could not be null or empty");
            }

            byte[] KeyArray = null;

            int KeyLengthNeed = KeySize / 8;

            KeyArray = Key.Length > KeyLengthNeed
                       ? Encoding.UTF8.GetBytes(Key.Substring(0, KeyLengthNeed))
                       : Encoding.UTF8.GetBytes(Key.PadRight(KeyLengthNeed, '0'));

            StorageFile EncryptedFile = null;
            try
            {
                EncryptedFile = await ExportFolder.CreateFileAsync($"{ Path.GetFileNameWithoutExtension(OriginFile.Name)}.sle", CreationCollisionOption.GenerateUniqueName);

                using (SecureString Secure = SecureAccessProvider.GetFileEncryptionAesIV(Package.Current))
                {
                    IntPtr Bstr = Marshal.SecureStringToBSTR(Secure);
                    string IV = Marshal.PtrToStringBSTR(Bstr);

                    try
                    {
                        using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
                        {
                            KeySize = KeySize,
                            Key = KeyArray,
                            Mode = CipherMode.CBC,
                            Padding = PaddingMode.Zeros,
                            IV = Encoding.UTF8.GetBytes(IV)
                        })
                        {
                            using (Stream OriginFileStream = await OriginFile.OpenStreamForReadAsync().ConfigureAwait(false))
                            using (Stream EncryptFileStream = await EncryptedFile.OpenStreamForWriteAsync().ConfigureAwait(false))
                            using (ICryptoTransform Encryptor = AES.CreateEncryptor())
                            {
                                byte[] Detail = Encoding.UTF8.GetBytes("$" + KeySize + "|" + OriginFile.FileType + "$");
                                await EncryptFileStream.WriteAsync(Detail, 0, Detail.Length).ConfigureAwait(false);

                                byte[] PasswordFlag = Encoding.UTF8.GetBytes("PASSWORD_CORRECT");
                                byte[] EncryptPasswordFlag = Encryptor.TransformFinalBlock(PasswordFlag, 0, PasswordFlag.Length);
                                await EncryptFileStream.WriteAsync(EncryptPasswordFlag, 0, EncryptPasswordFlag.Length).ConfigureAwait(false);

                                using (CryptoStream TransformStream = new CryptoStream(EncryptFileStream, Encryptor, CryptoStreamMode.Write))
                                {
                                    await OriginFileStream.CopyToAsync(TransformStream, 81920, CancelToken).ConfigureAwait(false);
                                    TransformStream.FlushFinalBlock();
                                }
                            }
                        }
                    }
                    finally
                    {
                        Marshal.ZeroFreeBSTR(Bstr);
                        unsafe
                        {
                            fixed (char* ClearPtr = IV)
                            {
                                for (int i = 0; i < IV.Length; i++)
                                {
                                    ClearPtr[i] = '\0';
                                }
                            }
                        }
                    }
                }

                return EncryptedFile;
            }
            catch (TaskCanceledException)
            {
                await EncryptedFile?.DeleteAsync(StorageDeleteOption.PermanentDelete);
                throw;
            }
            catch (CryptographicException)
            {
                await EncryptedFile?.DeleteAsync(StorageDeleteOption.PermanentDelete);
                throw;
            }
            catch (Exception)
            {
                await EncryptedFile?.DeleteAsync(StorageDeleteOption.PermanentDelete);
                return null;
            }
        }

        /// <summary>
        /// 根据指定的密钥，使用AES将文件解密至指定的文件夹
        /// </summary>
        /// <param name="EncryptedFile">要解密的文件</param>
        /// <param name="ExportFolder">指定解密文件的保存位置</param>
        /// <param name="Key">解密密钥</param>
        /// <param name="CancelToken">取消通知</param>
        /// <returns></returns>
        public static async Task<StorageFile> DecryptAsync(this StorageFile EncryptedFile, StorageFolder ExportFolder, string Key, CancellationToken CancelToken = default)
        {
            if (ExportFolder == null)
            {
                throw new ArgumentNullException(nameof(ExportFolder), "ExportFolder could not be null");
            }

            if (EncryptedFile == null)
            {
                throw new ArgumentNullException(nameof(EncryptedFile), "EncryptedFile could not be null");
            }

            if (string.IsNullOrEmpty(Key))
            {
                throw new ArgumentNullException(nameof(Key), "Key could not be null or empty");
            }

            StorageFile DecryptedFile = null;
            try
            {
                using (SecureString Secure = SecureAccessProvider.GetFileEncryptionAesIV(Package.Current))
                {
                    IntPtr Bstr = Marshal.SecureStringToBSTR(Secure);
                    string IV = Marshal.PtrToStringBSTR(Bstr);

                    try
                    {
                        using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
                        {
                            Mode = CipherMode.CBC,
                            Padding = PaddingMode.Zeros,
                            IV = Encoding.UTF8.GetBytes(IV)
                        })
                        {
                            using (Stream EncryptFileStream = await EncryptedFile.OpenStreamForReadAsync().ConfigureAwait(false))
                            {
                                byte[] DecryptByteBuffer = new byte[20];

                                await EncryptFileStream.ReadAsync(DecryptByteBuffer, 0, DecryptByteBuffer.Length).ConfigureAwait(false);

                                string FileType;
                                if (Encoding.UTF8.GetString(DecryptByteBuffer).Split('$', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() is string Info)
                                {
                                    string[] InfoGroup = Info.Split('|');
                                    if (InfoGroup.Length == 2)
                                    {
                                        int KeySize = Convert.ToInt32(InfoGroup[0]);
                                        FileType = InfoGroup[1];

                                        AES.KeySize = KeySize;

                                        int KeyLengthNeed = KeySize / 8;
                                        AES.Key = Key.Length > KeyLengthNeed ? Encoding.UTF8.GetBytes(Key.Substring(0, KeyLengthNeed)) : Encoding.UTF8.GetBytes(Key.PadRight(KeyLengthNeed, '0'));
                                    }
                                    else
                                    {
                                        throw new FileDamagedException("文件损坏，无法解密");
                                    }
                                }
                                else
                                {
                                    throw new FileDamagedException("文件损坏，无法解密");
                                }

                                DecryptedFile = await ExportFolder.CreateFileAsync($"{ Path.GetFileNameWithoutExtension(EncryptedFile.Name)}{FileType}", CreationCollisionOption.GenerateUniqueName);

                                using (Stream DecryptFileStream = await DecryptedFile.OpenStreamForWriteAsync().ConfigureAwait(false))
                                using (ICryptoTransform Decryptor = AES.CreateDecryptor(AES.Key, AES.IV))
                                {
                                    byte[] PasswordConfirm = new byte[16];
                                    EncryptFileStream.Seek(Info.Length + 2, SeekOrigin.Begin);
                                    await EncryptFileStream.ReadAsync(PasswordConfirm, 0, PasswordConfirm.Length).ConfigureAwait(false);

                                    if (Encoding.UTF8.GetString(Decryptor.TransformFinalBlock(PasswordConfirm, 0, PasswordConfirm.Length)) == "PASSWORD_CORRECT")
                                    {
                                        using (CryptoStream TransformStream = new CryptoStream(DecryptFileStream, Decryptor, CryptoStreamMode.Write))
                                        {
                                            await EncryptFileStream.CopyToAsync(TransformStream, 81920, CancelToken).ConfigureAwait(false);
                                            TransformStream.FlushFinalBlock();
                                        }
                                    }
                                    else
                                    {
                                        throw new PasswordErrorException("密码错误");
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        Marshal.ZeroFreeBSTR(Bstr);
                        unsafe
                        {
                            fixed (char* ClearPtr = IV)
                            {
                                for (int i = 0; i < IV.Length; i++)
                                {
                                    ClearPtr[i] = '\0';
                                }
                            }
                        }
                    }
                }

                return DecryptedFile;
            }
            catch (TaskCanceledException)
            {
                await DecryptedFile?.DeleteAsync(StorageDeleteOption.PermanentDelete);
                throw;
            }
            catch (CryptographicException)
            {
                await DecryptedFile?.DeleteAsync(StorageDeleteOption.PermanentDelete);
                throw;
            }
            catch (Exception)
            {
                await DecryptedFile?.DeleteAsync(StorageDeleteOption.PermanentDelete);
                return null;
            }
        }

        /// <summary>
        /// 根据类型寻找指定UI元素的子元素
        /// </summary>
        /// <typeparam name="T">寻找的类型</typeparam>
        /// <param name="root"></param>
        /// <returns></returns>
        public static T FindChildOfType<T>(this DependencyObject root) where T : DependencyObject
        {
            Queue<DependencyObject> ObjectQueue = new Queue<DependencyObject>();
            ObjectQueue.Enqueue(root);
            while (ObjectQueue.Count > 0)
            {
                DependencyObject Current = ObjectQueue.Dequeue();
                if (Current != null)
                {
                    for (int i = 0; i < VisualTreeHelper.GetChildrenCount(Current); i++)
                    {
                        var ChildObject = VisualTreeHelper.GetChild(Current, i);
                        if (ChildObject is T TypedChild)
                        {
                            return TypedChild;
                        }
                        ObjectQueue.Enqueue(ChildObject);
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 根据名称和类型寻找指定UI元素的子元素
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="root"></param>
        /// <param name="name">子元素名称</param>
        /// <returns></returns>
        public static T FindChildOfName<T>(this DependencyObject root, string name) where T : DependencyObject
        {
            Queue<DependencyObject> ObjectQueue = new Queue<DependencyObject>();
            ObjectQueue.Enqueue(root);
            while (ObjectQueue.Count > 0)
            {
                DependencyObject Current = ObjectQueue.Dequeue();
                if (Current != null)
                {
                    for (int i = 0; i < VisualTreeHelper.GetChildrenCount(Current); i++)
                    {
                        var ChildObject = VisualTreeHelper.GetChild(Current, i);
                        if (ChildObject is T TypedChild && (TypedChild as FrameworkElement).Name == name)
                        {
                            return TypedChild;
                        }
                        ObjectQueue.Enqueue(ChildObject);
                    }
                }
            }
            return null;
        }

        public static T FindParentOfType<T>(this DependencyObject child) where T : DependencyObject
        {
            T Parent = null;
            DependencyObject CurrentParent = VisualTreeHelper.GetParent(child);
            while (CurrentParent != null)
            {
                if (CurrentParent is T CParent)
                {
                    Parent = CParent;
                    break;
                }
                CurrentParent = VisualTreeHelper.GetParent(CurrentParent);
            }
            return Parent;
        }

        /// <summary>
        /// 删除当前文件夹下的所有子文件和子文件夹
        /// </summary>
        /// <param name="Folder">当前文件夹</param>
        /// <returns></returns>
        public static async Task DeleteAllSubFilesAndFolders(this StorageFolder Folder)
        {
            if (Folder == null)
            {
                throw new ArgumentNullException(nameof(Folder), "Folder could not be null");
            }

            foreach (var Item in await Folder.GetItemsAsync())
            {
                if (Item is StorageFolder folder)
                {
                    await DeleteAllSubFilesAndFolders(folder).ConfigureAwait(false);
                }
                else
                {
                    await Item.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
            }

            await Folder.DeleteAsync(StorageDeleteOption.PermanentDelete);
        }

        public static async Task<ulong> GetSizeRawDataAsync(this IStorageItem Item)
        {
            if (Item == null)
            {
                throw new ArgumentNullException(nameof(Item), "Item could not be null");
            }

            try
            {
                BasicProperties Properties = await Item.GetBasicPropertiesAsync();

                return Convert.ToUInt64(Properties.Size);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 获取存储对象的修改日期
        /// </summary>
        /// <param name="Item">存储对象</param>
        /// <returns></returns>
        public static async Task<DateTimeOffset> GetModifiedTimeAsync(this IStorageItem Item)
        {
            if (Item == null)
            {
                throw new ArgumentNullException(nameof(Item), "Item could not be null");
            }

            try
            {
                BasicProperties Properties = await Item.GetBasicPropertiesAsync();

                return Properties.DateModified;
            }
            catch
            {
                return DateTimeOffset.MinValue;
            }
        }

        /// <summary>
        /// 获取存储对象的缩略图
        /// </summary>
        /// <param name="Item">存储对象</param>
        /// <returns></returns>
        public static async Task<BitmapImage> GetThumbnailBitmapAsync(this IStorageItem Item)
        {
            try
            {
                if (Item is StorageFolder Folder)
                {
                    using (StorageItemThumbnail Thumbnail = await Folder.GetScaledImageAsThumbnailAsync(ThumbnailMode.ListView, 100))
                    {
                        if (Thumbnail == null || Thumbnail.Size == 0 || Thumbnail.OriginalHeight == 0 || Thumbnail.OriginalWidth == 0)
                        {
                            return null;
                        }

                        BitmapImage bitmapImage = new BitmapImage
                        {
                            DecodePixelHeight = 100,
                            DecodePixelWidth = 100
                        };
                        await bitmapImage.SetSourceAsync(Thumbnail);
                        return bitmapImage;
                    }
                }
                else if (Item is StorageFile File)
                {
                    using (CancellationTokenSource Cancellation = new CancellationTokenSource())
                    {
                        Task<StorageItemThumbnail> GetThumbnailTask = File.GetScaledImageAsThumbnailAsync(ThumbnailMode.ListView, 100).AsTask(Cancellation.Token);

                        bool IsSuccess = await Task.Run(() => SpinWait.SpinUntil(() => GetThumbnailTask.IsCompleted, 3000)).ConfigureAwait(true);

                        if (IsSuccess)
                        {
                            using (StorageItemThumbnail Thumbnail = GetThumbnailTask.Result)
                            {
                                if (Thumbnail == null)
                                {
                                    return null;
                                }

                                BitmapImage bitmapImage = new BitmapImage
                                {
                                    DecodePixelHeight = 100,
                                    DecodePixelWidth = 100
                                };
                                await bitmapImage.SetSourceAsync(Thumbnail);
                                return bitmapImage;
                            }
                        }
                        else
                        {
                            Cancellation.Cancel();

                            if (!ToastNotificationManager.History.GetHistory().Any((Toast) => Toast.Tag == "DelayLoadNotification"))
                            {
                                ToastContent Content = new ToastContent()
                                {
                                    Scenario = ToastScenario.Default,
                                    Launch = "Transcode",
                                    Visual = new ToastVisual()
                                    {
                                        BindingGeneric = new ToastBindingGeneric()
                                        {
                                            Children =
                                                {
                                                    new AdaptiveText()
                                                    {
                                                        Text = Globalization.GetString("DelayLoadNotification_Title")
                                                    },

                                                    new AdaptiveText()
                                                    {
                                                       Text = Globalization.GetString("DelayLoadNotification_Content_1")
                                                    },

                                                    new AdaptiveText()
                                                    {
                                                        Text = Globalization.GetString("DelayLoadNotification_Content_2")
                                                    }
                                                }
                                        }
                                    }
                                };
                                ToastNotification Notification = new ToastNotification(Content.GetXml())
                                {
                                    Tag = "DelayLoadNotification"
                                };
                                ToastNotificationManager.CreateToastNotifier().Show(Notification);
                            }

                            return null;
                        }
                    }
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 平滑滚动至指定的项
        /// </summary>
        /// <param name="listViewBase"></param>
        /// <param name="item">指定项</param>
        /// <param name="alignment">对齐方式</param>
        public static void ScrollIntoViewSmoothly(this ListViewBase listViewBase, object item, ScrollIntoViewAlignment alignment = ScrollIntoViewAlignment.Default)
        {
            if (listViewBase == null)
            {
                throw new ArgumentNullException(nameof(listViewBase), "listViewBase could not be null");
            }

            if (listViewBase.FindChildOfType<ScrollViewer>() is ScrollViewer scrollViewer)
            {
                double originHorizontalOffset = scrollViewer.HorizontalOffset;
                double originVerticalOffset = scrollViewer.VerticalOffset;

                void layoutUpdatedHandler(object sender, object e)
                {
                    listViewBase.LayoutUpdated -= layoutUpdatedHandler;

                    double targetHorizontalOffset = scrollViewer.HorizontalOffset;
                    double targetVerticalOffset = scrollViewer.VerticalOffset;

                    void scrollHandler(object s, ScrollViewerViewChangedEventArgs t)
                    {
                        scrollViewer.ViewChanged -= scrollHandler;

                        scrollViewer.ChangeView(targetHorizontalOffset, targetVerticalOffset, null);
                    }

                    scrollViewer.ViewChanged += scrollHandler;

                    scrollViewer.ChangeView(originHorizontalOffset, originVerticalOffset, null, true);
                }

                listViewBase.LayoutUpdated += layoutUpdatedHandler;

                listViewBase.ScrollIntoView(item, alignment);
            }
            else
            {
                listViewBase.ScrollIntoView(item, alignment);
            }
        }

        public static string ComputeMD5Hash(this string Data)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(Data));

                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < hash.Length; i++)
                {
                    _ = builder.Append(hash[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public static string ComputeMD5Hash(this Stream Stream)
        {
            using (MD5 md5 = MD5.Create())
            {
                Stream.Seek(0, SeekOrigin.Begin);

                byte[] hash = md5.ComputeHash(Stream);

                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < hash.Length; i++)
                {
                    _ = builder.Append(hash[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public async static Task<string> ComputeMD5Hash(this StorageFile File, CancellationToken Token)
        {
            using (MD5 md5 = MD5.Create())
            using (Stream stream = await File.OpenStreamForReadAsync().ConfigureAwait(false))
            {
                Token.Register((s) =>
                {
                    try
                    {
                        Stream Para = s as Stream;
                        Para.Dispose();
                    }
                    catch
                    {

                    }
                }, stream, false);

                return await Task.Run(() =>
                    {
                        try
                        {
                            byte[] hash = md5.ComputeHash(stream);

                            StringBuilder builder = new StringBuilder();

                            for (int i = 0; i < hash.Length; i++)
                            {
                                _ = builder.Append(hash[i].ToString("x2"));
                            }

                            return builder.ToString();
                        }
                        catch
                        {
                            return string.Empty;
                        }
                    }).ConfigureAwait(false);
            }
        }

        public async static Task<string> ComputeSHA1Hash(this StorageFile File, CancellationToken Token)
        {
            using (SHA1 SHA = SHA1.Create())
            using (Stream stream = await File.OpenStreamForReadAsync().ConfigureAwait(false))
            {
                Token.Register((s) =>
                {
                    try
                    {
                        Stream Para = s as Stream;
                        Para.Dispose();
                    }
                    catch
                    {

                    }
                }, stream, false);

                return await Task.Run(() =>
                    {
                        try
                        {
                            byte[] Hash = SHA.ComputeHash(stream);

                            StringBuilder builder = new StringBuilder();

                            for (int i = 0; i < Hash.Length; i++)
                            {
                                _ = builder.Append(Hash[i].ToString("x2"));
                            }

                            return builder.ToString();
                        }
                        catch
                        {
                            return string.Empty;
                        }
                    }).ConfigureAwait(false);
            }
        }

        public async static Task<string> ComputeSHA256Hash(this StorageFile File, CancellationToken Token)
        {
            using (SHA256 SHA = SHA256.Create())
            using (Stream stream = await File.OpenStreamForReadAsync().ConfigureAwait(false))
            {
                Token.Register((s) =>
                {
                    try
                    {
                        Stream Para = s as Stream;
                        Para.Dispose();
                    }
                    catch
                    {

                    }
                }, stream, false);

                return await Task.Run(() =>
                {
                    try
                    {
                        byte[] Hash = SHA.ComputeHash(stream);

                        StringBuilder builder = new StringBuilder();

                        for (int i = 0; i < Hash.Length; i++)
                        {
                            _ = builder.Append(Hash[i].ToString("x2"));
                        }

                        return builder.ToString();
                    }
                    catch
                    {
                        return string.Empty;
                    }
                }).ConfigureAwait(false);
            }
        }

        public async static Task<string> ComputeCrc32Hash(this StorageFile File, CancellationToken Token)
        {
            using (Crc32CAlgorithm Crc = new Crc32CAlgorithm(false))
            using (Stream stream = await File.OpenStreamForReadAsync().ConfigureAwait(false))
            {
                Token.Register((s) =>
                {
                    try
                    {
                        Stream Para = s as Stream;
                        Para.Dispose();
                    }
                    catch
                    {

                    }
                }, stream, false);

                return await Task.Run(() =>
                {
                    try
                    {
                        byte[] Hash = Crc.ComputeHash(stream);

                        StringBuilder builder = new StringBuilder();

                        for (int i = 0; i < Hash.Length; i++)
                        {
                            _ = builder.Append(Hash[i].ToString("x2"));
                        }

                        return builder.ToString();
                    }
                    catch
                    {
                        return string.Empty;
                    }
                }).ConfigureAwait(false);
            }
        }
    }
}
