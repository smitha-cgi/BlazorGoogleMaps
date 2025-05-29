using GoogleMapsComponents;
using GoogleMapsComponents.Maps;
using GoogleMapsComponents.Maps.Extension;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ServerSideDemo.Pages;

public class MapMarkerInfo
{
    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public string Icon { get; set; }

    public string Text { get; set; }

    public MapMarkerInfo(string text, double latitude, double longitude, string icon)
    {
        Latitude = latitude;
        Longitude = longitude;
        Icon = icon;
        Text = text;
    }
}

public partial class MapCircleListPage : ComponentBase
{
    private AdvancedGoogleMap _map = null!;
    private MapOptions _mapOptions = null!;
    private AdvancedMarkerElementList _markerElements;
    private int _bunchsize = 10;

    private List<MapMarkerInfo> _markers = [];
    private CircleList? _circleList;
    private readonly Dictionary<string, CircleOptions> _circleOptionsByRef = new Dictionary<string, CircleOptions>();
    private int _lastId;
    private PolygonList? _createedPolygons;

    public const string Svg = @"<svg xmlns=""http://www.w3.org/2000/svg"" width=""26"" height=""26"" viewBox=""0 0 30 30"">
        <circle cx=""15"" cy=""15"" r=""10"" stroke=""black"" fill=""green"" stroke-width=""1""/>
        </svg>";

    protected override void OnInitialized()
    {
        _mapOptions = new MapOptions()
        {
            Zoom = 13,
            Center = new LatLngLiteral()
            {
                Lat = 48.994249,
                Lng = 12.190451
            },
            MapTypeId = MapTypeId.Roadmap,
            MapId = "AxNykfeVms3x"
        };
    }

    /// <summary>
    /// Create a bunch of circles, put them into a dictionary with reference ids and display them on the map.
    /// </summary>
    private async void CreateBunchOfPolygon()
    {
        var outerCoords = new List<LatLngLiteral>()
        {
            new LatLngLiteral(13.501908279929077, 100.69801114196777),
            new LatLngLiteral(13.491392275719202, 100.74933789367675),
            new LatLngLiteral(13.465851481053091, 100.71637890930175),
        };

        var innerCoords = new List<LatLngLiteral>()
        {
            new LatLngLiteral(13.487386057049033, 100.72633526916503),
            new LatLngLiteral(13.48137660307361, 100.719125491333),
            new LatLngLiteral(13.478705686132331, 100.72959683532714),
        };

        _createedPolygons = await PolygonList.CreateAsync(_map.MapRef.JsRuntime, new Dictionary<string, PolygonOptions>()
        {
            { Guid.NewGuid().ToString(), new PolygonOptions()
            {
                Paths = new[] { outerCoords, innerCoords },
                Draggable = true,
                Editable = false,
                FillColor = "blue",
                ZIndex = 999,
                Visible = true,
                StrokeWeight = 5,
                Map = _map.InteropObject
            }}
        });
        var first = _createedPolygons.Polygons.First().Value;
        var path = await first.GetPath();
        await _map.InteropObject.SetCenter(path.First());
    }

    private async void CreateBunchOfCircles()
    {
        List<AdvancedMarkerElementOptions> circles = [];

        int howMany = _bunchsize;
        var bounds = await _map.InteropObject.GetBounds();
        double maxRadius = (bounds.North - bounds.South) * 111111.0 / (10 + Math.Sqrt(howMany));
        var colors = new[] { "#FFFFFF", "#9132D1", "#FFD800", "#846A00", "#AAC643", "#C96A00", "#B200FF", "#CD6A00", "#00A321", "#7F6420" };
        var rnd = new Random();
        for (int i = 0; i < howMany; i++)
        {
            string title = string.Format("Text{0}", i);
            double lat = bounds.South + rnd.NextDouble() * (bounds.North - bounds.South);
            double lon = bounds.West + rnd.NextDouble() * (bounds.East - bounds.West);

            var options = new MapMarkerInfo(title, lat, lon, Svg);

            _markers.Add(options);
        }

        _markerElements = await AdvancedMarkerElementList.CreateAsync(
            _map.MapRef.JsRuntime,
            _markers.ToDictionary(_ => Guid.NewGuid().ToString(), y => new AdvancedMarkerElementOptions()
            {
                Position = new LatLngLiteral() { Lat = y.Latitude, Lng = y.Longitude },
                Map = _map.InteropObject,
                GmpDraggable = false,
                Title = string.Format("{0}", y.Text),
                Content = Svg,
            })
            );

        await _markerElements.AddListeners<MouseEvent>(_markerElements.Markers.Keys.ToList(), "click", async (o, e) =>
        {
            await o.Stop();

            Console.WriteLine("Clicked an object");
        });
    }

    private async Task RefreshCircleList()
    {
        _circleList = await CircleList.SyncAsync(_circleList, _map.MapRef.JsRuntime, _circleOptionsByRef, async (_, sKey, _) =>
        {
            // Circle has been clicked --> delete it.
            _circleOptionsByRef.Remove(sKey);
            await RefreshCircleList();
        });


    }

    private async Task RemoveBunchOfPolygon()
    {
        if (_createedPolygons != null)
        {
            foreach (var markerListMarker in _createedPolygons.Polygons)
            {
                await markerListMarker.Value.SetMap(null);
            }

            await _createedPolygons.RemoveAllAsync();
        }
    }

    private async Task RemoveBunchOfCircles()
    {
        if (_markerElements != null)
        {
            Dictionary<string, GoogleMapsComponents.Maps.Map?> maps = [];
            foreach (var element in _markerElements.Markers)
            {
                maps.Add(element.Key, null);
                await element.Value.ClearListeners("click");
            }

            await _markerElements.SetMaps(maps);

            await _markerElements.RemoveAllAsync();
        }
    }
}
