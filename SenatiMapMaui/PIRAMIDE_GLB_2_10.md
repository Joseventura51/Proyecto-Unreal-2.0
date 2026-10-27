# SENATI Map 2.10.0 — Pirámide, modelo real aportado (.glb)

## Modelo del primer piso

El equipo aportó `Piramide 1 piso.glb`, exportado de Blender en glTF binario 2.0.
- 97 304 bytes; 60 mallas y 61 primitivas; materiales básicos, sin texturas externas.
- SHA-256: `4095a6c89d70be35457f7462a86fa75130bacf169c25c8d1c793fe9ea8efb949`.
- El original se almacena **sin alterar su geometría**, comprimido en `Resources/Raw/wwwroot/piramide-piso1.glb.gz` (8 476 bytes). MAUI lo descomprime en memoria al entrar al primer piso.
- El visor `pyramid-glb-viewer.js` interpreta geometrías glTF y las dibuja con WebGL, sin conectarse a CDN ni requerir librerías externas. El código del visor se inyecta al `HtmlWebViewSource`, porque ese tipo de fuente no resuelve automáticamente rutas relativas.
- La app carga el modelo solo cuando el estudiante abre **Pirámide → Piso 1 → Modelo 3D GLB**. Se solicita mediante `senatimaui://pyramid/model`; la app descomprime el recurso de forma local y lo envía a la WebView.

## Funciones
- Arrastrar para girar, pellizcar para acercar, botones de giro/zoom, **Isométrico**, **Planta** y **Centrar**.
- **Modelo 3D GLB**: representación auténtica del archivo enviado, sin ruta interior sobrepuesta.
- **Plano y rutas**: conserva las habitaciones y rutas esquemáticas anteriores, basadas aproximadamente en el plano EV-52.
- Los pisos 2 y 3 conservan la simulación anterior de espacios y conexiones.
- **Guardar piso** recuerda el modo 3D/plano, la orientación y el zoom del modelo, además de la planta elegida.

## Precauciones
- El GLB no incluye nombres de ambientes, puertas etiquetadas, coordenadas de referencia ni una red de rutas.
- El plano EV-52 es una fotografía de evacuación, y su geometría no está calibrada con el archivo GLB. No se dibujan rutas arbitrarias encima del modelo.
- La transición entre plantas y el ascensor siguen siendo simulaciones. El modelo del primer piso **no certifica seguridad ni rutas de evacuación**.
- Para navegación real: verificar escala/orientación, identificar ambientes, entradas y núcleos verticales en Blender/QGIS, trazar su grafo y validar las conexiones en el edificio.

## Verificaciones del proyecto
El workflow `.github/workflows/senati-map-android.yml` revisa:
1. Sintaxis JavaScript de la WebView y del visor GLB.
2. Integridad SHA-256 del GLB comprimido y estructura glTF: 60 mallas, 61 primitivas.
3. Rutas del campus QGIS y 41 ambientes conceptuales de la Pirámide.
4. Dibujo SVG de los tres niveles como alternativa si WebGL no está disponible.

La compilación e instalación física del APK deben verificarse con la ejecución real de GitHub Actions y un teléfono Android.
