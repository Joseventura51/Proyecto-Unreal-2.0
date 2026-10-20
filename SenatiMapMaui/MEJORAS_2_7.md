# SENATI Map 2.7 — navegación por caminos QGIS

Esta rama conserva el proyecto nativo .NET MAUI (Android) y su integración con el mapa web.

## Implementado en código
- Las dos líneas `Linea_5` de la exportación QGIS2Web se incluyen en el grafo peatonal, junto con los senderos existentes.
- Se bloquean las conexiones automáticas entre senderos si atraviesan huellas de edificios. Los pasos QGIS explícitos se respetan.
- Ruta más rápida con cola de prioridad (Dijkstra) y filtro opcional «Evitar escaleras cartografiadas».
- Búsqueda de destinos con nombre y mensajes cuando una instalación no está identificada.
- Referencias QR **por código de texto**: `SENATI:GPS:lat,lon` para fijar una referencia conocida; `SENATI:PLACE:nombre` para seleccionar un destino. La lectura de cámara no está incluida todavía.
- Capa SVG de respaldo sin conexión para edificios, senderos, ruta, ubicación, zoom y desplazamiento. La vista 2D con Leaflet puede seguir necesitando conexión para sus teselas de OpenStreetMap. La vista 3D con MapLibre usa recursos de internet.
- Vista 3D esquemática con huellas adicionales de edificios QGIS, pasos resaltados y botón de restablecer cámara. Las alturas son **ilustrativas**.
- BLE con calibración a 1 metro, anclas de ubicación fija y cálculo aproximado mediante tres o más beacons registrados, calibrados y detectados. Si la solución es inestable, se conserva la posición GPS. Escaneo periódico optativo durante navegación.
- Registro del piso del beacon y selector de nivel; NO se calculan rutas entre pisos sin planos internos, puertas, escaleras, ascensores y sus conexiones.

## Prueba de campo sugerida
1. Actualizar el APK de GitHub Actions, conceder ubicación y dispositivos cercanos.
2. Elegir un destino y verificar físicamente que el camino QGIS realmente es transitable y que no atraviesa muros.
3. Probar el filtro para evitar escaleras registradas.
4. Desactivar los datos móviles y verificar el mapa vectorial de respaldo. Volver a conectarse para probar 3D.
5. Crear códigos de señalética (contenido `SENATI:GPS:lat,lon`), introducirlos en la pestaña del mapa y comprobar coordenadas.
6. Registrar al menos tres emisores BLE fijos, asignar coordenadas **medidas en sitio**, calibrar a 1 m y activar el escaneo periódico. Comprobar la diferencia frente a posiciones de referencia medidas.
7. Evaluar rendimiento, fluidez de la cámara 3D y consumo de batería en el teléfono de demostración.

## Datos aún necesarios para completar navegación interior real
- Trazos por cada piso y ubicaciones verificadas de puertas de paso entre edificios.
- Posición de rampas, escaleras y ascensores para rutas accesibles y entre plantas.
- Nombres y localización de aulas y ambientes que aún no aparecen en las capas.
- Coordenadas medidas, ID y calibración de cada beacon, con pruebas sobre distintas orientaciones, afluencia y obstrucciones.
- Un lector QR nativo con cámara si se requiere escanear directamente desde la aplicación.

Las distancias BLE son aproximaciones y **no** deben presentarse como exactas. Las rutas no verificadas en campo son propuestas cartográficas, no instrucciones de seguridad.
