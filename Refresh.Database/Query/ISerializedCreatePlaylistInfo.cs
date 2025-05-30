﻿using Refresh.Database.Models;

namespace Refresh.Database.Query;

public interface ISerializedCreatePlaylistInfo
{
    string Name { get; }
    string Description { get; }
    string? Icon { get; }
    GameLocation? Location { get; }
}