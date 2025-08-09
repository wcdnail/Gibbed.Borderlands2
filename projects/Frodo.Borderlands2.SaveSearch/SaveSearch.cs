/* Copyright (c) 2025 Frodo
 *
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 *
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 *
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 *
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.IO;
using System.Linq;
using Gibbed.Borderlands2.FileFormats;
using Gibbed.Borderlands2.FileFormats.Items;
using Gibbed.Borderlands2.GameInfo;
using DeserializeSettings = Gibbed.Borderlands2.FileFormats.SaveFile.DeserializeSettings;
using Gibbed.Borderlands2.ProtoBufFormats.WillowTwoSave;
using System.Text;
using SystemGuid = System.Guid;

namespace SaveSearch
{
    public struct Utils
    {
        public static string ShortStr(string inp)
        {
            var dotp = inp.LastIndexOf('.');
            if (dotp == -1)
                return inp;

            return inp.Substring(++dotp);
        }
    }

    public class DataGeneral
    {
        public Platform         Platform;
        public SystemGuid       SaveGuid;
        public int            SaveGameId;
        public string        PlayerClass;
        public int              ExpLevel;
        public int             ExpPoints;
        public int        OverpowerLevel;
        public int             SyncLevel;
        public int    GeneralSkillPoints;
        public int SpecialistSkillPoints;
        public string      CharacterName;
        public string       SelectedHead;
        public string       SelectedSkin;

        public DataGeneral(WillowTwoPlayerSaveGame saveGame, Platform platform)
        {
            this.Platform = platform;
            this.SaveGuid = (SystemGuid)saveGame.SaveGuid;
            this.SaveGameId = saveGame.SaveGameId;

            this.PlayerClass = saveGame.PlayerClass;

            var expLevel = saveGame.ExpLevel;
            var expPoints = saveGame.ExpPoints;

            if (expPoints < 0)
                expPoints = 0;

            if (expLevel <= 0)
                expLevel = Math.Max(1, Experience.GetLevelForPoints(expPoints));

            this.ExpLevel = expLevel;
            this.ExpPoints = expPoints;
            this.OverpowerLevel = saveGame.NumOverpowerLevelsUnlocked.HasValue == false ? 0 : saveGame.NumOverpowerLevelsUnlocked.Value;
            this.GeneralSkillPoints = saveGame.GeneralSkillPoints;
            this.SpecialistSkillPoints = saveGame.SpecialistSkillPoints;
            this.CharacterName = Encoding.UTF8.GetString(saveGame.UIPreferences.CharacterName);
            this.SelectedHead = saveGame.AppliedCustomizations.Count > 0 ? saveGame.AppliedCustomizations[0] : "None";
            this.SelectedSkin = saveGame.AppliedCustomizations.Count > 4 ? saveGame.AppliedCustomizations[4] : "None";
        }

        public static string DumpFormat = "{0,5} {1,45} {2,32}";

        public static void DumpCaption()
        {
            Console.WriteLine(DumpFormat,
                "Level",
                "Name",
                "Type"
            );
        }

        public void Dump()
        {
            DumpCaption();

            Console.WriteLine(DumpFormat,
                this.ExpLevel,
                this.CharacterName,
                Utils.ShortStr(this.PlayerClass)
            );
        }
    }

    public struct DataWeapons
    {
        public static string GenerateDisplayName(string weaponTypePath, string prefixPartPath, string titlePartPath)
        {
            if (titlePartPath != "None" &&
                InfoManager.WeaponNameParts.TryGetValue(titlePartPath, out var titlePart) == true &&
                string.IsNullOrEmpty(titlePart.Name) == false)
            {
                var text = titlePart.Name;

                if (prefixPartPath != "None" &&
                    InfoManager.WeaponNameParts.TryGetValue(prefixPartPath, out var prefixPart) == true &&
                    string.IsNullOrEmpty(prefixPart.Name) == false)
                {
                    text = prefixPart.Name + " " + text;
                }

                return text;
            }

            return "Unknown Weapon";
        }

        static void DumpBroken(PackedWeaponData weapon, Exception ex)
        {
            Console.WriteLine("Broken: '{0}' [{1}]", weapon.InventorySerialNumber.ToString(), ex.Message);
        }

        public static string DumpFormat = "{0,5} {1,45} {2,32} {3,16} {4,28} {5,28} {6,45}";

        public static void DumpCaption()
        {
            Console.WriteLine(DumpFormat,
                "Level",
                "Name",
                "Type",
                "Vendor",
                "Grip",
                "Elemental",
                "Accessory1"
            );
        }

        public static bool Dump(BaseWeapon weapon, string searchPattern)
        {
            var displayName = GenerateDisplayName(weapon.WeaponType, weapon.PrefixPart, weapon.TitlePart);

            bool isMatch = searchPattern.Length < 1 ? true :
                displayName.Contains(searchPattern) ||
                weapon.WeaponType.Contains(searchPattern) ||
                weapon.Manufacturer.Contains(searchPattern) ||
                weapon.GripPart.Contains(searchPattern) ||
                weapon.ElementalPart.Contains(searchPattern) ||
                weapon.Accessory1Part.Contains(searchPattern)
            ;
            
            if (isMatch)
                Console.WriteLine(DumpFormat,
                    weapon.GameStage,
                    displayName,
                    Utils.ShortStr(weapon.WeaponType),
                    Utils.ShortStr(weapon.Manufacturer),
                    Utils.ShortStr(weapon.GripPart),
                    Utils.ShortStr(weapon.ElementalPart),
                    Utils.ShortStr(weapon.Accessory1Part)
                );

            return isMatch;
        }

        public static int Process(WillowTwoPlayerSaveGame saveGame, Platform platform, string searchPattern)
        {
            int matchCount = 0;

            foreach (var packedWeapon in saveGame.PackedWeaponData)
            {
                BackpackWeapon weapon;
                try
                {
                    weapon = (BackpackWeapon)BackpackDataHelper.Decode(packedWeapon.InventorySerialNumber, platform);
                }
                catch (Exception ex)
                {
                    DumpBroken(packedWeapon, ex);
                    continue;
                }

                var test = BackpackDataHelper.Encode(weapon, platform);
                if (packedWeapon.InventorySerialNumber.SequenceEqual(test) == false)
                {
                    throw new FormatException("backpack weapon reencode mismatch");
                }

                weapon.QuickSlot = packedWeapon.QuickSlot;
                weapon.Mark = packedWeapon.Mark;

                if (Dump(weapon, searchPattern))
                    ++matchCount;
            }

            return matchCount;
        }
    }

    public struct DataItems
    {
        public static string GenerateDisplayName(string itemPath, string prefixPartPath, string titlePartPath)
        {
            string name = null;
            bool hasFullName = false;

            if (itemPath != "None" && InfoManager.Items.TryGetValue(itemPath, out var item) == true)
            {
                name = item.Name;
                hasFullName = item.HasFullName;
            }

            if (hasFullName == false &&
                titlePartPath != "None" &&
                InfoManager.ItemNameParts.TryGetValue(titlePartPath, out var titlePart) == true)
            {
                name = titlePart.Name;
            }

            if (name != null &&
                prefixPartPath != "None" &&
                InfoManager.ItemNameParts.TryGetValue(prefixPartPath, out var prefixPart) == true &&
                string.IsNullOrEmpty(prefixPart.Name) == false)
            {
                name = prefixPart.Name + " " + name;
            }

            if (string.IsNullOrEmpty(name) == false)
                return name;

            return "Unknown Item";
        }

        static void DumpBroken(PackedItemData item, Exception ex)
        {
            Console.WriteLine("Broken: '{0}' {1}", item.InventorySerialNumber.ToString(), ex.Message);
        }

        public static string DumpFormat = "{0,5} {1,45} {2,32} {2,16}";

        public static void DumpCaption()
        {
            Console.WriteLine(DumpFormat,
                "Level",
                "Name",
                "Type",
                "Vendor"
            );
        }

        public static bool Dump(BaseItem item, string searchPattern)
        {
            ItemType type = ItemType.Unknown;
            if (InfoManager.Items.TryGetValue(item.Item, out var foundItem))
                type = foundItem.Type;

            var typeStr = type.ToString();
            var displayName = GenerateDisplayName(item.Item, item.PrefixPart, item.TitlePart);

            bool isMatch = searchPattern.Length < 1 ? true :
                displayName.Contains(searchPattern) ||
                typeStr.Contains(searchPattern) ||
                item.Manufacturer.Contains(searchPattern)
            ;

            if (isMatch)
                Console.WriteLine(DumpFormat,
                    item.GameStage,
                    displayName,
                    Utils.ShortStr(typeStr),
                    Utils.ShortStr(item.Manufacturer)
                );

            return isMatch;
        }

        public static int Process(WillowTwoPlayerSaveGame saveGame, Platform platform, string searchPattern)
        {
            int matchCount = 0;

            foreach (var packedItem in saveGame.PackedItemData)
            {
                //if (packedItem.Quantity < 0)
                //{
                //    Dump(packedItem);
                //    continue;
                //}

                BackpackItem item;
                try
                {
                    item = (BackpackItem)BackpackDataHelper.Decode(packedItem.InventorySerialNumber, platform);
                }
                catch (Exception ex)
                {
                    DumpBroken(packedItem, ex);
                    continue;
                }

                var test = BackpackDataHelper.Encode(item, platform);
                if (packedItem.InventorySerialNumber.SequenceEqual(test) == false)
                {
                    throw new FormatException("backpack item reencode mismatch");
                }

                item.Quantity = packedItem.Quantity;
                item.Equipped = packedItem.Equipped;
                item.Mark = (PlayerMark)packedItem.Mark;

                // required since protobuf is no longer doing the validation for us
                if (item.Mark != PlayerMark.Trash &&
                    item.Mark != PlayerMark.Standard &&
                    item.Mark != PlayerMark.Favorite)
                {
                    throw new FormatException("invalid PlayerMark value");
                }

                if (Dump(item, searchPattern))
                    ++matchCount;
            }

            return matchCount;
        }
    }

    public struct DataBankItems
    {
        static void DumpBroken(BankSlot slot, Exception ex)
        {
            Console.WriteLine("Broken: '{0}' {1}", slot.InventorySerialNumber.ToString(), ex.Message);
        }

        public static int Process(WillowTwoPlayerSaveGame saveGame, Platform platform, string searchPattern)
        {
            int matchCount = 0;

            foreach (var bankSlot in saveGame.BankSlots)
            {
                IPackableSlot slot;
                try
                {
                    slot = BaseDataHelper.Decode(bankSlot.InventorySerialNumber, platform);
                }
                catch (Exception e)
                {
                    DumpBroken(bankSlot, e);
                    continue;
                }

                var test = BaseDataHelper.Encode(slot, platform);
                if (bankSlot.InventorySerialNumber.SequenceEqual(test) == false)
                {
                    throw new FormatException("bank slot reencode mismatch");
                }

                if (slot is BaseWeapon weapon)
                {
                    if (DataWeapons.Dump(weapon, searchPattern))
                        ++matchCount;
                }
                else if (slot is BaseItem item)
                {
                    if (DataItems.Dump(item, searchPattern))
                        ++matchCount;
                }
            }

            return matchCount;
        }
    }

    public class DataStorage
    {
        WillowTwoPlayerSaveGame Save;
        Platform Platform;

        public DataStorage(WillowTwoPlayerSaveGame saveGame, Platform platform)
        {
            this.Save = saveGame;
            this.Platform = platform;
        }

        public void Dump(string searchPattern)
        {
            int matchCount = 0;

            Console.WriteLine("Backpack Weapons:");
            matchCount += DataWeapons.Process(this.Save, this.Platform, searchPattern);

            Console.WriteLine("Backpack Items:");
            matchCount += DataItems.Process(this.Save, this.Platform, searchPattern);

            Console.WriteLine("Bank Items:");
            matchCount += DataBankItems.Process(this.Save, this.Platform, searchPattern);

            if (searchPattern.Length > 0)
                Console.WriteLine("Matches found: {0}", matchCount);
        }
    }

    public class DataSave
    {
        string   SaveFilename;
        SaveFile         Save;
        DataGeneral   General;
        DataStorage   Storage;
        string  SearchPattern;

        public DataSave(string filename, SaveFile saveFile, string searchPattern)
        {
            this.SaveFilename = filename;
            this.Save = saveFile;
            this.SearchPattern = searchPattern;

            this.General = new DataGeneral(this.Save.SaveGame, this.Save.Platform);
            this.Storage = new DataStorage(this.Save.SaveGame, this.Save.Platform);
        }

        public void Dump()
        {
            Console.WriteLine("---------------------------------------------------------------------------------------------------------------------------------------------------------------");
            Console.WriteLine("Save file '{0}' {1}", this.SaveFilename,
                this.SearchPattern.Length > 0 ? "searching: '" + this.SearchPattern + "'" : ""
            );
            this.General.Dump();
            this.Storage.Dump(this.SearchPattern);
            Console.WriteLine("");
        }
    }

    public class SaveCli
    {
        public static SaveFile ReadSave(string fileName, Platform platform)
        {
            SaveFile saveFile = null;
            using (var input = File.OpenRead(fileName))
                saveFile = SaveFile.Deserialize(input, platform, DeserializeSettings.None);

            SaveExpansion.ExtractExpansionSavedataFromUnloadableItemData(saveFile.SaveGame);
            return saveFile;
        }

        private static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: SaveSearch <pathname> [pattern]");
                return -1;
            }

            string inputPathname = args[0];
            string searchPattern = args.Length > 1 ? args[1] : "";
            string[] paths;

            try
            {
                var attr = File.GetAttributes(inputPathname);
                if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    paths = Directory.GetFiles(inputPathname, "*.sav");
                }
                else
                {
                    paths = new string[1];
                    paths[0] = inputPathname;
                }

                foreach (var path in paths)
                {
                    var saveFile = ReadSave(path, Platform.PC);
                    var saveData = new DataSave(path, saveFile, searchPattern);
                    saveData.Dump();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0} process failed:\n{1}",
                    inputPathname,
                    ex.ToString()
                );
            }

            return 0;
        }
    }
}
