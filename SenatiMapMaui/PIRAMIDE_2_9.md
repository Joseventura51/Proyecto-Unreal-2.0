# SENATI Map 2.9.0 — Pirámide con tres pisos y mobiliario isométrico

## Uso en Android
1. Abre la pestaña **Pirámide** o escribe *Pirámide* / *Dirección Nacional* en la búsqueda del mapa.
2. Selecciona **Piso 1**, **Piso 2** o **Piso 3** para inspeccionar cada planta de la maqueta.
3. Usa **◇ Isométrico** o **▣ Planta**, los controles de giro, zoom y **Centrar**.
4. Toca un ambiente para elegirlo. También puedes usar el desplegable con grupos por piso y el botón **Ver ruta**.
5. Selecciona **Ascensor** o **Escaleras**; usa **↑ / ↓** para representar manualmente el cambio de nivel. Tocar un ascensor o escalera dibujados también activa una transición.
6. Pulsa **▶ Simular recorrido**. Un indicador azul comienza en la entrada del piso 1, sigue los pasillos ilustrativos y sube de nivel hasta el destino elegido.
7. Pulsa **Guardar piso** para conservar el piso, la habitación seleccionada, la perspectiva, el zoom y el tipo de conexión vertical. Se usa `Preferences` de .NET MAUI.

## Zonas de cada piso
- **Piso 1**: recepción, sala de espera, oficinas, administración, archivo, servicios, sala de reuniones y dirección.
- **Piso 2**: oficinas de proyectos, administración, salas de conferencias, área colaborativa, sala de sistemas y servicios.
- **Piso 3**: dirección ejecutiva, gerencia, directorio, salón de visitas, secretaría y servicios.

Todos los pisos incluyen núcleos de escaleras y ascensor.

## Objetos decorativos representados
Geometría SVG proyectada con volumen isométrico: mobiliario de oficina, mesas de reunión, sillas, sofás, mostradores, monitores, estanterías, baños, iluminación, macetas, vegetación y estructura de los núcleos verticales. Todo está dibujado localmente dentro de la WebView; no se descargan modelos ni bibliotecas 3D extra.

## Alcance y limitaciones
- **Maqueta ilustrativa**: la distribución y las conexiones son ficticias, basadas en una propuesta visual para la Pirámide, **no en levantamiento arquitectónico real**. No sirven para evacuación ni navegación física verificada.
- Las alturas y el volumen se representan como **isometría SVG (2.5D)**, no como mallas tridimensionales de Blender/Unity.
- La navegación de demostración no usa GPS, BLE ni las rutas amarillas QGIS del campus.
- Para convertirla en navegación interior real, es imprescindible levantar el plano auténtico de cada planta, puertas, pasillos, escaleras, ascensor y coordenadas de entrada.

## Validación previa a compilación
El workflow `.github/workflows/senati-map-android.yml` valida la sintaxis del JavaScript, el grafo peatonal QGIS y que los 36 espacios en los tres pisos tengan rutas de ejemplo y transiciones compatibles.

Publicación: el pipeline de GitHub Actions genera el APK de MAUI al recibir cambios en `senati-map-build`. La ejecución y la instalación en Android deben confirmarse por separado.
