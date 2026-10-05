# Text color tags

Names, short names, and descriptions passed to `CreateItemLocale` support paired effects:

```text
<dwv-rainbow>{MyVariant.Name}</dwv-rainbow>
<dwv-gradient=#FF0000,#0000FF>{MyVariant.Description}</dwv-gradient>
```

Both gradient colors accept six-digit RGB hex values, with or without `#`.
The `{locale.key}` references resolve before coloring, so each language uses its
own visible character count. Gradients hold solid colors at the beginning and end,
blending across the middle. Spaces and rich-text tags do not advance the gradient.
Effects can cover part of a field, span multiple lines, and contain formatting such
as `<b>` or `<i>`. Nested effects and existing `<color>` sections keep their inner
colors. Invalid or unclosed effect tags remain unchanged.

`ItemGenerator` and `WeaponGenerator` apply bold rarity colors to names, using the
rainbow tag for Unique items. `CreateItemLocale` only resolves locale and effect tags.
