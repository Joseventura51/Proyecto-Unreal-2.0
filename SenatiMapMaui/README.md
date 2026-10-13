# SENATI Map — .NET MAUI

Esta carpeta contiene la versión real en **.NET MAUI** del prototipo SENATI Map.

## Arquitectura

- **MainPage.xaml**: pantalla nativa MAUI con un WebView.
- **Resources/Raw/wwwroot/index.html**: Leaflet + OpenStreetMap + geometría QGIS + rutas.
- **MauiLocationTrackingService.cs**: ubicación gestionada con APIs de .NET MAUI.
- **AndroidBleScannerService.cs**: BLE escrito en C# dentro de la capa Android de MAUI.
- **MainPage.xaml.cs**: puente entre JavaScript y C#.

La interfaz web envía comandos con el esquema interno `senatimaui://`. MAUI los intercepta desde `WebView.Navigating`. Las nuevas posiciones y lecturas BLE se mandan de C# a JavaScript con `EvaluateJavaScriptAsync`.

## Funciones conservadas

- OpenStreetMap real.
- Capas QGIS.
- Seguimiento de ubicación.
- Suavizado GPS y snap a caminos.
- Rutas y navegación paso a paso.
- Edificios seleccionables.
- Vista 3D demostrativa.
- iBeacon/Eddystone.
- Filtrado RSSI y rangos BLE.

## Visual Studio 2022

1. Instala la carga de trabajo **Desarrollo de interfaz de usuario de aplicaciones multiplataforma de .NET (MAUI)**.
2. Abre `SenatiMapMaui.sln` o `SenatiMapMaui.csproj`.
3. Selecciona Android.
4. Ejecuta en Debug.

El identificador de esta versión es `pe.edu.senati.senatimap.maui`, así que puede coexistir con el APK Android anterior durante las pruebas.
