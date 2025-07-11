﻿// This class contains common classes and types that can't exist in both Realm and Postgres builds.
// Along with any other Realm compatibility steps, this will be removed when Postgres is fully complete.

namespace Refresh.Database.Compatibility;

#if POSTGRES
public interface IRealmObject;

[AttributeUsage(AttributeTargets.Property)]
public class IgnoredAttribute : Attribute;

[AttributeUsage(AttributeTargets.Property)]
public class PrimaryKeyAttribute : Attribute;

[AttributeUsage(AttributeTargets.Property)]
public class IndexedAttribute : Attribute
{
    public IndexedAttribute()
    {}

    public IndexedAttribute(IndexType type)
    {
        _ = type;
    }
}

public enum IndexType
{
    FullText,
}

#else
[AttributeUsage(AttributeTargets.Property)]
public class KeyAttribute : Attribute;

public class NotMappedAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class)]
public class IndexAttribute : Attribute
{
    public IndexAttribute(params string[] parameterNames)
    {
        _ = parameterNames;
    }
}

[AttributeUsage(AttributeTargets.Property)]
public class ForeignKeyAttribute : Attribute
{
    public ForeignKeyAttribute(string key)
    {
        _ = key;
    }
}
#endif