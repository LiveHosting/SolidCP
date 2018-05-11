﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using System.Threading.Tasks;

namespace SolidCP.Providers.Virtualization
{
    public static class HardDriveHelper
    {
        public static VirtualHardDiskInfo[] Get(PowerShellManager powerShell, string vmname)
        {
            List<VirtualHardDiskInfo> disks = new List<VirtualHardDiskInfo>();

            Collection<PSObject> result = GetPS(powerShell, vmname);

            if (result != null && result.Count > 0)
            {
                foreach (PSObject d in result)
                {
                    VirtualHardDiskInfo disk = new VirtualHardDiskInfo();

                    disk.SupportPersistentReservations = Convert.ToBoolean(d.GetProperty("SupportPersistentReservations"));
                    disk.MaximumIOPS = Convert.ToUInt64(d.GetProperty("MaximumIOPS"));
                    disk.MinimumIOPS = Convert.ToUInt64(d.GetProperty("MinimumIOPS"));
                    disk.VHDControllerType = d.GetEnum<ControllerType>("ControllerType");
                    disk.ControllerNumber = Convert.ToInt32(d.GetProperty("ControllerNumber"));
                    disk.ControllerLocation = Convert.ToInt32(d.GetProperty("ControllerLocation"));
                    disk.Path = d.GetProperty("Path").ToString();
                    disk.Name = d.GetProperty("Name").ToString();

                    GetVirtualHardDiskDetail(powerShell, disk.Path, ref disk);

                    disks.Add(disk);
                }
            }
            return disks.ToArray();
        }

        //public static VirtualHardDiskInfo GetByPath(PowerShellManager powerShell, string vhdPath)
        //{
        //    VirtualHardDiskInfo info = null;
        //    var vmNames = new List<string>();

        //    Command cmd = new Command("Get-VM");

        //    Collection<PSObject> result = powerShell.Execute(cmd, true);

        //    if (result == null || result.Count == 0)
        //        return null;

        //    vmNames = result.Select(r => r.GetString("Name")).ToList();
        //    var drives = vmNames.SelectMany(n => Get(powerShell, n));

        //    return drives.FirstOrDefault(d=>d.Path == vhdPath);
        //}

        public static Collection<PSObject> GetPS(PowerShellManager powerShell, string vmname)
        {
            Command cmd = new Command("Get-VMHardDiskDrive");
            cmd.Parameters.Add("VMName", vmname);

            return powerShell.Execute(cmd, true);
        }

        public static void GetVirtualHardDiskDetail(PowerShellManager powerShell, string path, ref VirtualHardDiskInfo disk)
        {
            if (!string.IsNullOrEmpty(path))
            {
                Command cmd = new Command("Get-VHD");
                cmd.Parameters.Add("Path", path);
                Collection<PSObject> result = powerShell.Execute(cmd, true);
                if (result != null && result.Count > 0)
                {
                    disk.DiskFormat = result[0].GetEnum<VirtualHardDiskFormat>("VhdFormat");
                    disk.DiskType = result[0].GetEnum<VirtualHardDiskType>("VhdType");
                    disk.ParentPath = result[0].GetProperty<string>("ParentPath");
                    disk.MaxInternalSize = Convert.ToInt64(result[0].GetProperty("Size"));
                    disk.FileSize = Convert.ToInt64(result[0].GetProperty("FileSize"));
                    disk.Attached = disk.InUse = Convert.ToBoolean(result[0].GetProperty("Attached"));
                }
            }
        }

        public static void Update(PowerShellManager powerShell, VirtualMachine vm, int HddSize) //TODO rework the code
        {
            Collection<PSObject> result = GetPS(powerShell, vm.Name);


            if (result != null && result.Count > 0)
            {
                foreach (PSObject d in result)
                {
                    VirtualHardDiskInfo disk = new VirtualHardDiskInfo();

                    disk.SupportPersistentReservations = Convert.ToBoolean(d.GetProperty("SupportPersistentReservations"));
                    disk.MaximumIOPS = Convert.ToUInt64(d.GetProperty("MaximumIOPS"));
                    disk.MinimumIOPS = Convert.ToUInt64(d.GetProperty("MinimumIOPS"));
                    disk.VHDControllerType = d.GetEnum<ControllerType>("ControllerType");
                    disk.ControllerNumber = Convert.ToInt32(d.GetProperty("ControllerNumber"));
                    disk.ControllerLocation = Convert.ToInt32(d.GetProperty("ControllerLocation"));
                    disk.Path = d.GetProperty("Path").ToString();
                    disk.Name = d.GetProperty("Name").ToString();

                    Command cmd = new Command("Resize-VHD");
                    cmd.Parameters.Add("SizeBytes", HddSize * Constants.Size1G);
                    cmd.Parameters.Add("Path", disk.Path);
                    powerShell.Execute(cmd, true);
                }
            }
        }

        public static void SetIOPS(PowerShellManager powerShell, VirtualMachine vm, int minIOPS, int maxIOPS)
        {
            //TODO
            //*********Move checks in the Enterprise Server methods?*********//
            int maxPossibleIOPS = 1000000000;
            if (maxIOPS > maxPossibleIOPS)
                maxIOPS = maxPossibleIOPS;
            bool disableIOPS = (maxIOPS == 0 && minIOPS == 0);
            bool isIOPScorrect = (minIOPS <= maxIOPS) || ((minIOPS > maxIOPS) && (maxIOPS == 0));
            //***************************************************************//

            if (vm.Disks != null && isIOPScorrect)
            {
                foreach (VirtualHardDiskInfo disk in vm.Disks)
                {
                    Command cmd = new Command("Set-VMHardDiskDrive");
                    cmd.Parameters.Add("VMName", vm.Name);
                    cmd.Parameters.Add("ControllerType", disk.VHDControllerType.ToString());
                    cmd.Parameters.Add("ControllerNumber", disk.ControllerNumber);
                    cmd.Parameters.Add("ControllerLocation", disk.ControllerLocation);
                    if (disableIOPS){
                        cmd.Parameters.Add("MinimumIOPS", false);
                        cmd.Parameters.Add("MaximumIOPS", false);
                    }else{
                        cmd.Parameters.Add("MinimumIOPS", minIOPS);
                        cmd.Parameters.Add("MaximumIOPS", maxIOPS);
                    }
                    powerShell.Execute(cmd, true, true);
                }
            }
        }

        public static void Delete(PowerShellManager powerShell, VirtualHardDiskInfo[] disks)
        {
            if (disks != null && disks.GetLength(0) > 0)
            {
                foreach (VirtualHardDiskInfo disk in disks)
                {
                    Command cmd = new Command("Remove-item");
                    cmd.Parameters.Add("path", disk.Path);
                    powerShell.Execute(cmd, true);
                }
            }
        }
    }
}
