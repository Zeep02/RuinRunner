-- Hand-placed hero candidates, one character per pixel. Run with: aseprite -b --script candidates.lua
local INK = Color{r=26,g=20,b=36}
local function rgb(hex) return Color{r=tonumber(hex:sub(1,2),16),g=tonumber(hex:sub(3,4),16),b=tonumber(hex:sub(5,6),16)} end
local heroes = {
  knight = { pal={s="dfe6ee",S="9fb0c4",d="66778f",y="ffe27a",r="d8483f",R="9c2c36",b="5c3a2e"}, rows={
    "................",
    ".....kkkkkk.....",
    "...kkssssSSkk...",
    "..kssssssSSSdk..",
    "..kssssssSSSdk..",
    "..kkkkkkkkkkkk..",
    "..kkkykkkkykkk..",
    "..kkkykkkkykkk..",
    "..kSSSSSSSSddk..",
    "...kkSSSSddkk...",
    "...krrrrrrRRk...",
    "..krrrrrrrRRRk..",
    "..krrrrrrrRRRk..",
    "...kkrrrRRRkk...",
    "....kbbkkbbk....",
    "....kkkkkkkk....",
  }},
  hood = { pal={h="3f5f9e",H="2c4279",d="1e2b56",f="f6e7cf",o="f59a3c",O="c9652a",b="6b4a3a"}, rows={
    "................",
    ".....kkkkkk.....",
    "....khhhhhHk....",
    "...khhhhhhHHk...",
    "..khhhkkkkkHHk..",
    "..khhkffffffkk..",
    "..khkfkffkffkk..",
    "..khkfkffkffkk..",
    "..kHkffffffkHk..",
    "...kHkkkkkkHk...",
    "...koooooOOOk...",
    "..kHHoooOOHHdk..",
    "..kHHHHHHHHddk..",
    "...kkHHHHddkk...",
    "....kbbkkbbk....",
    "....kkkkkkkk....",
  }},
  pot = { pal={m="b9c2cc",M="808b99",d="525c6c",f="ffd9b0",F="e0a97f",g="4f9a5c",G="326f46",b="5c3a2e"}, rows={
    "................",
    "....kkkkkkkk....",
    "...kmmmmmMMMk...",
    "..kkkkkkkkkkkk..",
    ".kddddddddddddk.",
    ".kkkkkkkkkkkkkk.",
    "..kffffffffFFk..",
    "..kffkfffkfFFk..",
    "..kffkfffkfFFk..",
    "...kkffffFFkk...",
    "...kggggggGGk...",
    "..kggggggGGGGk..",
    "..kggggggGGGGk..",
    "...kkgggGGGkk...",
    "....kbbkkbbk....",
    "....kkkkkkkk....",
  }},
  mask = { pal={w="f4efe2",W="c9c2b2",p="5a2a7c",P="3c1a5c",y="ffcf4a",t="2a1f4f",b="1f1733"}, rows={
    ".....k....k.....",
    "....kpk..kPk....",
    "...kppkkkkPPk...",
    "..kpppppppPPPk..",
    "..kpkwwwwwwkPk..",
    "..kkwwwwwwwWkk..",
    "..kkwkkwwkkWkk..",
    "..kkwkywwkyWkk..",
    "..kkwwwwwwWWkk..",
    "...kkwwWWWWkk...",
    "...kPPPPPPPPk...",
    "..kPPPyPPPPPtk..",
    "..kPPPPPPPtttk..",
    "...kkPPPtttkk...",
    "....kbbkkbbk....",
    "....kkkkkkkk....",
  }},
  sprout = { pal={l="8bd14f",L="4f9e3a",c="f5e9cc",C="d6c3a0",o="d65f45",O="a53f3a",b="5c3a2e"}, rows={
    "......k..k......",
    ".....klkklLk....",
    "......kllLk.....",
    "....kkkkkkkk....",
    "...kccccccCCk...",
    "..kccccccccCCk..",
    "..kcckcccckCCk..",
    "..kcckcccckCCk..",
    "..kccccccccCCk..",
    "...kkcccCCCkk...",
    "...kooooooOOk...",
    "..koooooooOOOk..",
    "..koooooooOOOk..",
    "...kkoooOOOkk...",
    "....kbbkkbbk....",
    "....kkkkkkkk....",
  }},
}
local out = app.fs.joinPath(app.fs.filePath(app.params["self"] or "."), "out")
out = app.params["out"]
for name, hero in pairs(heroes) do
  local sprite = Sprite(16, 16, ColorMode.RGB)
  local image = Image(16, 16, ColorMode.RGB)
  for y, row in ipairs(hero.rows) do
    for x = 1, #row do
      local ch = row:sub(x, x)
      if ch == "k" then image:drawPixel(x - 1, y - 1, INK)
      elseif ch ~= "." then image:drawPixel(x - 1, y - 1, rgb(hero.pal[ch])) end
    end
  end
  sprite.cels[1].image = image
  sprite:saveAs(app.fs.joinPath(out, name .. ".aseprite"))
  sprite:saveCopyAs(app.fs.joinPath(out, name .. ".png"))
  sprite:close()
  print("wrote " .. name)
end
