using System.Xml.Serialization;
using Refresh.Database.Query;
using Refresh.GameServer.Types.Levels;

namespace Refresh.Database.Models.Users;

[XmlRoot("updateUser")]
public class SerializedUpdateDataProfile : SerializedUpdateData {}

[XmlRoot("user")]
public class SerializedUpdateDataPlanets : SerializedUpdateData {}

[Ignored]
public class SerializedUpdateData : ISerializedEditUser
{
    [XmlElement("biography")]
    public string? Description { get; set; }
    
    [XmlElement("location")]
    public GameLocation? UserLocation { get; set; }

    [XmlArray("slots")]
    public List<SerializedLevelLocation>? LevelLocations { get; set; }
    
    [XmlElement("planets")]
    public string? PlanetsHash { get; set; }
    
    [XmlElement("icon")]
    public string? IconHash { get; set; }
    
    [XmlElement("yay2")]
    public string? YayFaceHash { get; set; }
    
    [XmlElement("boo2")]
    public string? BooFaceHash { get; set; }

    [XmlElement("meh2")]
    public string? MehFaceHash { get; set; }
}