# colorsys

```python
import colorsys
```

Conversion functions between RGB and other color systems (HSV, HLS, YIQ).

All values are floats in the range [0.0, 1.0]. Hue is expressed as a fraction of the full circle (not degrees).

## Functions

### `colorsys.rgb_to_hsv(r: float, g: float, b: float) -> tuple[float, float, float]`

Convert the color from RGB coordinates to HSV coordinates.

### `colorsys.hsv_to_rgb(h: float, s: float, v: float) -> tuple[float, float, float]`

Convert the color from HSV coordinates to RGB coordinates.

### `colorsys.rgb_to_hls(r: float, g: float, b: float) -> tuple[float, float, float]`

Convert the color from RGB coordinates to HLS coordinates.

### `colorsys.hls_to_rgb(h: float, l: float, s: float) -> tuple[float, float, float]`

Convert the color from HLS coordinates to RGB coordinates.

### `colorsys.rgb_to_yiq(r: float, g: float, b: float) -> tuple[float, float, float]`

Convert the color from RGB coordinates to YIQ coordinates.

### `colorsys.yiq_to_rgb(y: float, i: float, q: float) -> tuple[float, float, float]`

Convert the color from YIQ coordinates to RGB coordinates.
