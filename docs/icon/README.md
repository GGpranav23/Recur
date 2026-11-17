# App icon sources

`HabitApp/Recur.ico` contains nine sizes (16, 20, 24, 32, 40, 48, 64, 128 and 256 px). Each size is drawn from the design that looks sharpest at that size:

| Source | Used for |
|---|---|
| `icon-16.svg` | 16 and 20 px, a simplified design aligned to a 16 px grid |
| `icon-24.svg` | 24 and 32 px, aligned to a 24 px grid |
| `icon.svg` | 40, 48, 64, 128 and 256 px, the full design |

To rebuild the icon after editing the SVGs, you need `rsvg-convert` and Python with Pillow:

```sh
cd docs/icon
rsvg-convert -w 16  icon-16.svg -o 16.png
rsvg-convert -w 20  icon-16.svg -o 20.png
rsvg-convert -w 24  icon-24.svg -o 24.png
rsvg-convert -w 32  icon-24.svg -o 32.png
for s in 40 48 64 128 256; do rsvg-convert -w $s icon.svg -o $s.png; done
python build_ico.py ../../HabitApp/Recur.ico 16.png 20.png 24.png 32.png 40.png 48.png 64.png 128.png 256.png
rm *.png
```
