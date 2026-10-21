using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmix.Domain.Attendance.Zones;

namespace Cosmix.Application.Attendance.Zones;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ZoneBoundary(ZoneBoundaryKind Kind,decimal? Latitude,decimal? Longitude,decimal? RadiusMeters,string? PolygonGeoJson);
public enum ZonePosition { Inside=1, Outside=2, Uncertain=3 }
public sealed record ZoneBoundaryResult(ZonePosition Position,bool NearBoundary);

/// <summary>Bounded site geometry. Polygon segments use GeoJSON linear longitude/latitude interpolation.
/// Holes, antimeridian/polar shapes and polygons spanning more than one degree are explicitly unsupported.</summary>
public static class ZoneGeometry
{
    private const double EarthRadius=6371008.8;
    public static void Validate(ZoneBoundary value)
    {
        if(value is null || !Enum.IsDefined(value.Kind)) throw new ArgumentException("A supported boundary is required.");
        if(value.Kind==ZoneBoundaryKind.Circle)
        {
            if(value.Latitude is null || value.Longitude is null || value.RadiusMeters is null or <=0 or >100000 || value.PolygonGeoJson is not null)
                throw new ArgumentException("A bounded circle requires centre and radius only.");
            Coordinates(value.Latitude.Value,value.Longitude.Value); return;
        }
        if(value.Latitude.HasValue || value.Longitude.HasValue || value.RadiusMeters.HasValue) throw new ArgumentException("A polygon has no circle fields.");
        _=Polygon(value.PolygonGeoJson);
    }
    public static ZoneBoundaryResult Evaluate(ZoneBoundary boundary,decimal latitude,decimal longitude,decimal accuracyMeters,decimal warningDistanceMeters)
    {
        Validate(boundary); Coordinates(latitude,longitude);
        if(accuracyMeters<0 || warningDistanceMeters<0) throw new ArgumentException("Distances cannot be negative.");
        double signed;
        if(boundary.Kind==ZoneBoundaryKind.Circle)
            signed=(double)boundary.RadiusMeters!.Value-Distance((double)latitude,(double)longitude,(double)boundary.Latitude!.Value,(double)boundary.Longitude!.Value);
        else
        {
            var points=Polygon(boundary.PolygonGeoJson); var inside=false; var lat=(double)latitude; var lon=(double)longitude; var distance=double.MaxValue;
            for(var i=0;i<points.Length-1;i++)
            {
                var a=points[i]; var b=points[i+1];
                if((a.Lat>lat)!=(b.Lat>lat) && lon<(b.Lon-a.Lon)*(lat-a.Lat)/(b.Lat-a.Lat)+a.Lon) inside=!inside;
                // On a bounded simple local segment the distance is unimodal. Endpoints are checked too.
                var low=0d; var high=1d;
                double At(double t)=>Distance(lat,lon,a.Lat+(b.Lat-a.Lat)*t,a.Lon+(b.Lon-a.Lon)*t);
                for(var iteration=0;iteration<48;iteration++) { var l=low+(high-low)/3; var r=high-(high-low)/3; if(At(l)<At(r)) high=r; else low=l; }
                distance=Math.Min(distance,Math.Min(Math.Min(At(0),At(1)),At((low+high)/2)));
            }
            signed=inside?distance:-distance;
        }
        // Two centimetres is numerical tolerance, not a configurable warning or accuracy policy.
        var uncertainty=(double)accuracyMeters+0.02;
        var position=Math.Abs(signed)<=uncertainty?ZonePosition.Uncertain:signed>0?ZonePosition.Inside:ZonePosition.Outside;
        return new(position,Math.Abs(signed)<=uncertainty+(double)warningDistanceMeters);
    }
    private static (double Lon,double Lat)[] Polygon(string? json)
    {
        try
        {
            if(string.IsNullOrWhiteSpace(json) || json.Length>32000) throw new ArgumentException("A bounded polygon is required.");
            using var document=JsonDocument.Parse(json,new JsonDocumentOptions { MaxDepth=5 });
            var root=document.RootElement;
            if(root.GetProperty("type").GetString()!="Polygon") throw new ArgumentException("A Polygon is required.");
            var rings=root.GetProperty("coordinates");
            if(rings.GetArrayLength()!=1) throw new ArgumentException("Only one simple exterior ring is supported.");
            var ring=rings[0]; if(ring.GetArrayLength() is <4 or >129) throw new ArgumentException("Use 3 to 128 vertices and repeat the first point.");
            var points=new List<(double Lon,double Lat)>();
            foreach(var p in ring.EnumerateArray())
            {
                if(p.GetArrayLength()!=2) throw new ArgumentException("Use two-dimensional positions.");
                var lon=p[0].GetDecimal(); var lat=p[1].GetDecimal(); Coordinates(lat,lon);
                if(Math.Abs(lat)>=85) throw new ArgumentException("Polar site polygons are unsupported.");
                points.Add(((double)lon,(double)lat));
            }
            if(points[0]!=points[^1] || points.Take(points.Count-1).Distinct().Count()!=points.Count-1 ||
                points.Max(p=>p.Lon)-points.Min(p=>p.Lon)>1 || points.Max(p=>p.Lat)-points.Min(p=>p.Lat)>1)
                throw new ArgumentException("A closed, non-repeating local ring is required.");
            double Cross((double Lon,double Lat) a,(double Lon,double Lat) b,(double Lon,double Lat) c)=>
                (b.Lon-a.Lon)*(c.Lat-a.Lat)-(b.Lat-a.Lat)*(c.Lon-a.Lon);
            var area=0d;
            for(var i=0;i<points.Count-1;i++)
            {
                area+=points[i].Lon*points[i+1].Lat-points[i+1].Lon*points[i].Lat;
                for(var j=i+2;j<points.Count-1;j++)
                {
                    if(i==0 && j==points.Count-2) continue;
                    var a=points[i]; var b=points[i+1]; var c=points[j]; var d=points[j+1];
                    var ab1=Cross(a,b,c); var ab2=Cross(a,b,d); var cd1=Cross(c,d,a); var cd2=Cross(c,d,b);
                    if(Math.Max(Math.Min(a.Lon,b.Lon),Math.Min(c.Lon,d.Lon))<=Math.Min(Math.Max(a.Lon,b.Lon),Math.Max(c.Lon,d.Lon)) &&
                       Math.Max(Math.Min(a.Lat,b.Lat),Math.Min(c.Lat,d.Lat))<=Math.Min(Math.Max(a.Lat,b.Lat),Math.Max(c.Lat,d.Lat)) &&
                       ab1*ab2<=0 && cd1*cd2<=0) throw new ArgumentException("Polygon edges cannot cross or touch.");
                }
            }
            if(Math.Abs(area)<1e-12) throw new ArgumentException("Polygon area must be positive.");
            return points.ToArray();
        }
        catch(Exception e) when(e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        { throw new ArgumentException("Invalid site polygon.",e); }
    }
    private static void Coordinates(decimal lat,decimal lon)
    { if(lat is < -90 or >90 || lon is < -180 or >180) throw new ArgumentException("Invalid coordinates."); }
    private static double Distance(double lat1,double lon1,double lat2,double lon2)
    {
        var r=Math.PI/180; var x=Math.Sin((lat2-lat1)*r/2); var y=Math.Sin((lon2-lon1)*r/2);
        return 2*EarthRadius*Math.Asin(Math.Sqrt(Math.Clamp(x*x+Math.Cos(lat1*r)*Math.Cos(lat2*r)*y*y,0,1)));
    }
}

public static class ZoneRules
{
    public static bool Warning(ZoneEntryRule rule,ZonePosition position,bool nearBoundary)=>rule switch
    {
        ZoneEntryRule.Permitted=>position!=ZonePosition.Inside || nearBoundary,
        ZoneEntryRule.Prohibited=>position!=ZonePosition.Outside || nearBoundary,
        _=>throw new ArgumentException("Only Operational and Red zones are supported.")
    };
}
