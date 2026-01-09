# CopyOpsSuite

Suite de copia y cobro en vivo para operaciones de duplicacion USB con precios por volumen (CUP+), auditoria y trazabilidad. Incluye modo bandeja, mini ventanas por destino y ajustes de tiers con CAP.

## Caracteristicas principales

- Copia multi-destino con progreso en vivo
- Cobro en vivo por volumen con tiers y CAP (1 TB = precio fijo)
- Vista mini por destino con cobro ahora/final
- Modo bandeja (tray) con menu rapido
- Auditoria y eventos operativos
- Editor de precios por volumen (CUP+)

## Requisitos

- Windows 10/11
- .NET 8 SDK

## Compilacion y ejecucion

```powershell
# desde la raiz del repo
cd CopyOpsSuite
dotnet build .\CopyOpsSuite.sln

# ejecutar la app
cd CopyOpsSuite\src\App.WinUI
dotnet run
```

## Estructura

- `src/App.WinUI`: UI (WinUI 3)
- `src/Core`: motor, modelos, almacenamiento y auditoria
- `docs`: especificaciones

## Licencia

MIT. Ver `LICENSE`.
