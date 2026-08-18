# ReGreen Data Contract

Bu doküman, AI, backend ve frontend tarafında kullanılacak ortak veri alanlarını tanımlar.

## Temel Alanlar

| Alan                  | Açıklama                                     |
| --------------------- | -------------------------------------------- |
| `fire_id`             | Yangının benzersiz kimliği                   |
| `grid_id`             | Haritadaki grid hücresinin benzersiz kimliği |
| `latitude`            | Hücre merkezinin enlemi                      |
| `longitude`           | Hücre merkezinin boylamı                     |
| `burn_severity`       | Yangın şiddeti                               |
| `slope`               | Eğim bilgisi                                 |
| `elevation`           | Yükselti                                     |
| `tree_cover`          | Ağaç / bitki örtüsü oranı                    |
| `road_distance`       | En yakın yola mesafe                         |
| `water_distance`      | Su kaynağına mesafe                          |
| `settlement_distance` | Yerleşim alanına mesafe                      |
| `recovery_score`      | Model tarafından üretilen toparlanma skoru   |
| `recovery_class`      | Toparlanma sınıfı                            |
| `priority_score`      | Müdahale / değerlendirme öncelik skoru       |
| `priority_class`      | Öncelik sınıfı                               |

## Ortak Sınıf İsimleri

### Recovery

- `LOW`
- `MEDIUM`
- `HIGH`

### Priority

- `LOW`
- `MEDIUM`
- `HIGH`

## Örnek Veri

```json
{
  "fire_id": "manavgat_2021",
  "grid_id": "MNG_001",
  "latitude": 36.786,
  "longitude": 31.442,
  "burn_severity": 0.72,
  "slope": 18.5,
  "elevation": 420,
  "tree_cover": 0.58,
  "road_distance": 750,
  "water_distance": 1200,
  "settlement_distance": 2400,
  "recovery_score": 0.31,
  "recovery_class": "LOW",
  "priority_score": 0.81,
  "priority_class": "HIGH"
}
```
