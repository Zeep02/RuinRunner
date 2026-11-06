-- Import native individual frames without resampling. Called with -b --script-param manifest=...
local source = assert(app.params.manifest, "manifest parameter required")
local handle = assert(io.open(source, "r"))
local manifest = json.decode(handle:read("*a"))
handle:close()
local sprite = Sprite(manifest.width, manifest.height, ColorMode.RGB)
sprite.layers[1].name = "Body"
for i, frame in ipairs(manifest.frames) do
  if i > 1 then sprite:newEmptyFrame() end
  local image = Image{fromFile=frame.path}
  assert(image.width == manifest.width and image.height == manifest.height, "Native size mismatch")
  sprite:newCel(sprite.layers[1], sprite.frames[i], image, Point(0, 0))
  sprite.frames[i].duration = frame.duration_ms / 1000
end
for _, info in ipairs(manifest.tags) do
  local tag = sprite:newTag(info.from + 1, info.to + 1)
  tag.name = info.name
end
sprite:saveAs(manifest.output)
print("Saved " .. manifest.output .. " with " .. #sprite.frames .. " frames")
