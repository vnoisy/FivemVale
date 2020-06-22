﻿using CitizenFX.Core;
using static CitizenFX.Core.Native.API;
using System;
using System.Threading.Tasks;
using CitizenFX.Core.UI;
using MenuAPI;
using static MenuAPI.MenuItem;
using Newtonsoft.Json;
using static MenuAPI.MenuCheckboxItem;

namespace Client
{
    public class Main : BaseScript
    {
        private static int blip;
        private static bool eventStart = false;
        private static bool fastVale = false;
        private static Menu menu;
        private static Ped driver;
        private static int driverId;
        private static Vector3 spawnPos;
        private static Vector3 targetLoc;
        private static dynamic ESX;
        private static int networkCar;
        private static string plate;
        private static ConfigModel config = new ConfigModel();
        private static MenuCheckboxItem box;
        private static int price;
        public Main()
        {
            Tick += OnTick;
            Tick += OnNoDelayTick;
            
            var data = LoadResourceFile(GetCurrentResourceName(), "config.json");
            try
            {
                config = JsonConvert.DeserializeObject<ConfigModel>(data);
                price = config.ValePrice;
            }
            catch (Exception e)
            {
                Debug.WriteLine("Config cannot loaded!!");
                Debug.WriteLine("Bcs: " + e.Message);
            }
            MenuSelector();
        }
        private void MenuSelector()
        {
            MenuController.MenuAlignment = MenuController.MenuAlignmentOption.Right;

            menu = new Menu(config.Locales.MenuTitle, config.Locales.MenuSubTitle);

            menu.OnItemSelect += (_menu, _item, _index) =>
            {
                if (box.Checked)
                {
                    price += config.FastValePrice;
                    FastVale(_item.ItemData);
                }
                else
                {
                    price = config.FastValePrice;
                    Vale(_item.ItemData);
                }
            };

            menu.OnMenuOpen += (_menu) =>
            {
                _menu.ClearMenuItems();
                ESX.TriggerServerCallback("esx_advancedgarage:getOwnedCars", new Action<dynamic>(ownedCars =>
                {
                    var count = ownedCars.Count;
                    Debug.WriteLine(count.ToString());
                    if (count == 0)
                    {
                        ESX.ShowNotification(config.Locales.NoCarGarage);
                        return;
                    }

                    for (int i = 0; i < count; i++)
                    {
                        var v = ownedCars[i];
                        try
                        {
                            var hashCar = (uint)v.vehicle.model;
                            var aheadVehName = GetDisplayNameFromVehicleModel(hashCar);
                            var vehicleName = GetLabelText(aheadVehName);
                            var storedText = config.Locales.StoredTextNotReady;
                            if (v.stored) storedText = config.Locales.StoredTextReady;

                            var model = new Model((int)hashCar);

                            if (!model.IsBicycle)
                            {
                                var menuItem = new MenuItem(vehicleName)
                                {
                                    Description = v.plate + " " + storedText,
                                    LeftIcon = model.IsBike ? Icon.BIKE : Icon.CAR,
                                    ItemData = v,
                                    Enabled = v.stored,
                                };
                                menu.AddMenuItem(menuItem);
                            }
                        }

                        catch (Exception e)
                        {
                            Debug.WriteLine(e.Message);
                            Debug.WriteLine("Error with this vehicle: " + v.vehicle.model.ToString());
                        }
                    }

                }));

                if (config.FastValeService)
                {
                    box = new MenuCheckboxItem(config.Locales.FastValeCheckBoxName, config.Locales.FastValeCheckBoxDescName, false);
                    box.Style = CheckboxStyle.Tick;
                    menu.AddMenuItem(box);
                }

            };

            menu.OnCheckboxChange += (_menu, _item, _index, _checked) =>
            {
                if (_item == box)
                {
                    if (_checked)
                    {
                        _item.Text = $"{config.Locales.FastValeCheckBoxName} +{config.FastValePrice} $";
                    }
                    else
                    {
                        _item.Text = config.Locales.FastValeCheckBoxName;
                    }
                }
            };

            MenuController.MenuToggleKey = (Control)config.MenuToggleKey;
            MenuController.AddMenu(menu);
        }

        private async Task OnNoDelayTick()
        {
            if (eventStart && driver != null)
            {
                //driverType = new Model(GetEntityModel(networkCar)).IsBike ? 37 : 36;
                DrawMarker(36, targetLoc.X, targetLoc.Y, targetLoc.Z + 1f, 0f, 0f, 0f, 0f, 0f, 0f, 3f, 3f, 3f, 255, 255, 255, 255, false, false, 2, true, null, null, false);
            }
            if (eventStart && fastVale)
            {
                DrawMarker(36, targetLoc.X, targetLoc.Y, targetLoc.Z + 1f, 0f, 0f, 0f, 0f, 0f, 0f, 3f, 3f, 3f, 255, 255, 255, 255, false, false, 2, true, null, null, false);
            }
        }
        private async Task OnTick()
        {
            while (ESX == null)
            {
                TriggerEvent("esx:getSharedObject", new object[] { new Action<dynamic>(esx => {
                    ESX = esx;
                })});
                await Delay(1000);
            }

            if (eventStart && driver != null && driver.IsInVehicle())
            {
                await Delay(500);
                Status();
            }

            if (eventStart && fastVale)
            {
                await Delay(500);
                FastStatus();
            }
        }
        private void Status()
        {
            var pos = Game.PlayerPed.Position;
            if (GetDistanceBetweenCoords(pos.X, pos.Y, pos.Z, driver.Position.X, driver.Position.Y, driver.Position.Z, false) > 150f)
            {
                ESX.ShowNotification(config.Locales.WhileTransferFailing);
                Abort();
            }

            if (GetDistanceBetweenCoords(pos.X, pos.Y, pos.Z, driver.Position.X, driver.Position.Y, driver.Position.Z, false) < 2f)
            {
                //driver.Task.LeaveVehicle(LeaveVehicleFlags.LeaveDoorOpen);
                ESX.ShowNotification(config.Locales.ComplateText);
                TriggerServerEvent("v_Vale:Pay", price);
                ESX.ShowNotification(price + "$ " + config.Locales.ValePaidedText);
                Complate();
            }
        }
        private void FastStatus()
        {
            if (Game.PlayerPed.IsInVehicle())
            {
                HalfAbort();
            }
        }
        private void Abort()
        {
            RemoveBlip(ref blip);
            eventStart = false;
            DeleteEntity(ref networkCar);
            DeleteEntity(ref driverId);
            driver = null;
            TriggerServerEvent("esx_advancedgarage:setVehicleState", plate, true);
            return;
        }
        private void Complate()
        {
            RemoveBlip(ref blip);
            eventStart = false;
            DeleteEntity(ref driverId);
            driver = null;
            return;
        }
        private void HalfAbort()
        {
            RemoveBlip(ref blip);
            eventStart = false;
            fastVale = false;
            return;
        }
        private void FastVale(dynamic v)
        {
            if (eventStart)
            {
                ESX.ShowNotification(config.Locales.ValeAldreadyUsingError);
                return;
            }
            var pos = Game.PlayerPed.Position;
            var pHeading = Game.PlayerPed.Heading;
            targetLoc = new Vector3();
            GetRoadSidePointWithHeading(pos.X, pos.Y, pos.Z, pHeading, ref targetLoc);
            ESX.Game.SpawnVehicle(v.vehicle.model, targetLoc, pHeading, new Action<dynamic>(callback_vh =>
            {
                ESX.Game.SetVehicleProperties(callback_vh, v.vehicle);
                blip = AddBlipForEntity((int)callback_vh);
                SetBlipColour(blip, 40);
                BeginTextCommandSetBlipName("STRING");
                AddTextComponentString("Vale");
                EndTextCommandSetBlipName(blip);
            }));
            TriggerServerEvent("esx_advancedgarage:setVehicleState", v.vehicle.plate, false);
            ESX.ShowNotification(config.Locales.ComplateText);
            TriggerServerEvent("v_Vale:Pay", price);
            ESX.ShowNotification(price + "$ " + config.Locales.ValePaidedText);
            eventStart = true;
            fastVale = true;
        }
        private async void Vale(dynamic v)
        {
            if (eventStart)
            {
                ESX.ShowNotification(config.Locales.ValeAldreadyUsingError);
                return;
            }

            var pos = Game.PlayerPed.Position;
            var pHeading = Game.PlayerPed.Heading;
            spawnPos = new Vector3();
            var spawnHeading = 0F;
            var unused = 0;
            GetNthClosestVehicleNodeWithHeading(pos.X, pos.Y, pos.Z, 80, ref spawnPos, ref spawnHeading, ref unused, 9, 3.0F, 2.5F);

            var pedModel = new Model(PedHash.Andreas);
            driver = await World.CreatePed(pedModel, spawnPos, spawnHeading);
            driverId = driver.Handle;
            targetLoc = new Vector3();
            GetRoadSidePointWithHeading(pos.X, pos.Y, pos.Z, pHeading, ref targetLoc);
            await Delay(100);

            ESX.Game.SpawnVehicle(v.vehicle.model, spawnPos, spawnHeading, new Action<dynamic>(async callback_vh =>
            {
                networkCar = (int)callback_vh;
                ESX.Game.SetVehicleProperties(callback_vh, v.vehicle);
                blip = AddBlipForEntity(networkCar);
                SetBlipColour(blip, 40);
                BeginTextCommandSetBlipName("STRING");
                AddTextComponentString("Vale");
                EndTextCommandSetBlipName(blip);

                await Delay(100);
                TaskEnterVehicle(driver.Handle, networkCar, -1, -1, 1f, 16, 0);

                await Delay(100);
                TaskVehicleDriveToCoord(driver.Handle, networkCar, targetLoc.X, targetLoc.Y, targetLoc.Z, 15f, 1, 0, 782, 2f, 1f);
            }));
            TriggerServerEvent("esx_advancedgarage:setVehicleState", v.vehicle.plate, false);
            plate = v.vehicle.plate.ToString();
            menu.CloseMenu();

            if (GetDistanceBetweenCoords(pos.X, pos.Y, pos.Z, driver.Position.X, driver.Position.Y, driver.Position.Z, false) > 150f)
            {
                ESX.ShowNotification(config.Locales.ValeCannotUsingThisPos);
                Abort();
                return;
            }
            ESX.ShowNotification(config.Locales.ValeOnTheWay);
            //networkCar = GetVehiclePedIsIn(driver.Handle, false);
            eventStart = true;
        }

    }
}