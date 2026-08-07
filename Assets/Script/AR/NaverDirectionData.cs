using System;
using System.Collections.Generic;

[Serializable]
public class Root
{
    public Route route;
}

[Serializable]
public class Route
{
    public Traoptimal[] traoptimal;
}

[Serializable]
public class Traoptimal
{
    public List<List<double>> path;
}