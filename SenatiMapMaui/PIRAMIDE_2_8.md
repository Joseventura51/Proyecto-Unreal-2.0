# SenatiMap 2.8 — Pirámide Dirección Nacional (prototipo de piso)

## Qué incluye

El apartado **Pirámide** se incorporó al mismo HTML WebView de la aplicación MAUI original (`Resources/Raw/wwwroot/index.html`) y no reemplaza el mapa QGIS2Web, el GPS, las rutas amarillas ni el módulo BLE.

- Vista **isométrica interactiva** del piso y modo de **planta superior** sin conexión a internet.
- Accesos por la pestaña **Pirámide**, **Opciones → Explorar piso 3D**, la búsqueda *Pirámide* / *Dirección Nacional*, y toque de un edificio 3D **solo si su nombre cartográfico lo identifica como Dirección Nacional o Pirámide**.
- Ambientes ilustrativos: recepción, sala de espera, oficina oeste, administración, archivo, baños, escaleras oeste/este, ascensor, sala de reuniones, dirección y oficina este.
- Selección táctil de habitaciones y simulación de rutas desde la entrada. Las rutas siguen un **grafo interior inventado solo para esta maqueta**, independiente del grafo GPS/QGIS del campus; la app nunca lo mezcla con rutas reales.
- Controles: rotación, zoom, centrar, vista superior/isométrica y **Guardar piso** (almacenamiento local con Preferences de MAUI).
- Interfaz liviana: dibujo SVG vectorial generado desde geometrías de ejemplo, sin nuevas bibliotecas ni descargas necesarias.

## Cómo probar

1. Instala el APK generado por el workflow de la rama `senati-map-build`.
2. Pulsa **Pirámide** en la barra inferior.
3. Selecciona **Dirección** o toca cualquier ambiente; usa **Ver ruta** para representar el recorrido desde la entrada.
4. Alterna **Isométrico** y **Planta**; gira, acerca o aleja, y pulsa **Centrar**.
5. Pulsa **Guardar piso**. Vuelve a abrir el aplicativo para probar la restauración.
6. Regresa a **Mapa** y comprueba que GPS, BLE y rutas QGIS sigan apareciendo.

## Límites importantes

**No es un plano arquitectónico auténtico.** La imagen conceptual generada anteriormente se utilizó como inspiración visual, pero el piso se volvió a dibujar como esquema SVG navegable; no representa distribución, número de plantas, dimensiones, puertas, accesos ni rutas de evacuación verificados de Dirección Nacional.

No se han inventado coordenadas geográficas de la Pirámide para colocarla en una posición del campus. La asociación geográfica real requiere verificar el edificio sobre QGIS.

Para que esta simulación se convierta en un navegador interior real se necesitan planos levantados por nivel y ubicaciones comprobadas de escaleras, ascensor, entradas y cada ambiente. Las rutas de demostración no deben emplearse para desplazamiento real hasta completar ese levantamiento.
