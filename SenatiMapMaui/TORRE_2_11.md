# Torre 2.11.0

Tocar Torres/Centro Tecnológico en el mapa 2D, mapa local o vista 3D abre el modelo aportado. Buscar Torre también lo abre. La vista mantiene giro y zoom con gestos y un botón para volver al mapa. No añade pisos ficticios.

Origen: torre.glb, SHA-256 8d1e9df15adbd63be26ec742c09ea81f13829d1b63190c770f3edb4a4d42fc16, 107188124 bytes. Recurso móvil: Resources/Raw/wwwroot/torre.glb.gz, 7276921 bytes, descomprimido 8612000 bytes, SHA-256 f21b2069ebece7c39dcc37a333b4ffdade826f6840350265a0ce9a0550538d73. Las imágenes JPEG se redujeron a 1024 px y calidad 85. Las seis mallas, nueve primitivas, accesores, índices, UV y transformaciones mantienen los datos originales. Se verificó igualdad byte a byte de todas las vistas de búfer que no son imágenes.

El visor local dibuja las texturas de color base y una iluminación simple; conserva en el recurso los mapas de normal y rugosidad, pero no realiza sombreado PBR completo. No usa CDN. El modelo se carga bajo demanda desde MauiAsset y se transfiere en fragmentos de 131072 caracteres para evitar una llamada JavaScript de varios MB. El visor ya cargado se reutiliza al volver a abrirlo.

Validación: validate-glb.cjs comprueba los destinos de búfer WebGL, las nueve superficies y las tres texturas de color; validate-tower.cjs comprueba nombres, apertura, reensamblado exacto de fragmentos, caché y estado de error. Se mantienen las comprobaciones del campus y la Pirámide. Falta validación física en Android.
