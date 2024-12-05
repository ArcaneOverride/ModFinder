using ModFinder.Mod;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Markup;

namespace ModFinder.UI
{
  // Supported columns for custom sorting
  [TypeConverter(typeof(EnumConverter))] 
  public enum SortColumn : Int32
  {
    Enabled = 0,
    Name,
    Author,
    LastUpdated,
    Status,
  }

  [MarkupExtensionReturnType(typeof(int))]
  public class EnumToIntExtension : MarkupExtension
  {
    public EnumToIntExtension() { }
    public EnumToIntExtension(Enum enumValue) { EnumValue = enumValue; }
    [ConstructorArgument("enumValue")]
    public Enum EnumValue { get; set; }
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
      return int.Parse(EnumValue.ToString("d"));
    }
  }

  internal class ModSort : IComparer, IComparer<ModViewModel>
  {
    private readonly SortColumn Column;
    private readonly bool Invert;
    private ModSort Subsort;

    public ModSort(SortColumn column, bool invert, ModSort subsort = null)
    {
      Column = column;
      Invert = invert;
      if (subsort != null)
        Subsort = subsort;
      RemoveSubsort(column);
      
    }

    private void RemoveSubsort(SortColumn column)
    {
      if(Subsort != null)
      {
        if (Subsort.Column == column)
        {
          Subsort = Subsort.Subsort;
          RemoveSubsort(column);
        }
        else
          Subsort.RemoveSubsort(column);
      }
    }

    public int Compare(object x, object y)
    {
      var modelX = x as ModViewModel;
      var modelY = y as ModViewModel;
      if (x is null || y is null)
        throw new ArgumentException("Can only sort ModViewModel.");

      return Compare(modelX, modelY);
    }

    public int Compare(ModViewModel x, ModViewModel y)
    {
      return Invert ? CompareInternal(y, x) : CompareInternal(x, y);
    }
    private int CompareInternal(ModViewModel x, ModViewModel y)
    {
      int result = Column switch
      {
        SortColumn.Enabled => x.Enabled.CompareTo(y.Enabled),
        SortColumn.Status => CompareStatus(x, y),
        SortColumn.Name => CompareStrings(x.Name, y.Name),
        SortColumn.Author => CompareStrings(x.Author, y.Author),
        SortColumn.LastUpdated => CompareStrings(x.LastUpdated, y.LastUpdated),
        _ => throw new ArgumentException($"Unsupported column for sorting: {Column}"),
      };

      if (result == 0)
        return CompareSubsort(x, y);
      return result;
    }

    private int CompareSubsort(ModViewModel x, ModViewModel y)
    {
      return Subsort != null ? Subsort.Compare(x,y) : 0;
    }

    private int CompareStrings(string x, string y)
    {
      if (x == y)
        return 0;
      if (string.IsNullOrEmpty(x))
        return 1;
      if (string.IsNullOrEmpty(y))
        return -1;
      return x.CompareTo(y);
    }

    private int CompareStatus(ModViewModel x, ModViewModel y)
    {
      var statusX = GetStatus(x);
      var statusY = GetStatus(y);
      if (statusX == statusY)
        return 0;
      return statusX - statusY;
    }

    private static Status GetStatus(ModViewModel mod)
    {
      if (mod.InstallState == InstallState.Installing)
        return Status.Installing;
      if (!mod.IsInstalled && !mod.IsCached)
        return Status.Uninstalled;
      if (!mod.IsInstalled && mod.IsCached)
        return Status.Cached;
      if (mod.InstalledVersion < mod.Latest.Version)
        return Status.UpdateAvailable;
      if (mod.MissingRequirements.Any())
        return Status.MissingRequirements;
      return Status.Installed;
    }

    // Numbers are assigned to create priority for easy sorting
    private enum Status
    {
      MissingRequirements = 1,
      UpdateAvailable = 2,
      Installing = 3,
      Installed = 4,
      Cached = 5,
      Uninstalled = 6,
    }
  }
}
