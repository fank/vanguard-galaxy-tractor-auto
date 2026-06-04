TFM      := netstandard2.1
CONFIG   := Debug
DLL      := VGTractorAuto.dll

BUILDDIR := VGTractorAuto/bin/$(CONFIG)/$(TFM)
BUILDDLL := $(BUILDDIR)/$(DLL)

# WSL path to the game install — adjust if Steam lives elsewhere
GAME_DIR := /mnt/c/Program Files (x86)/Steam/steamapps/common/Vanguard Galaxy
PLUGIN_DIR := $(GAME_DIR)/BepInEx/plugins/VGTractorAuto

# Sibling VGTTS checkout owns the canonical publicized Assembly-CSharp.dll stub.
VGTTS_LIB := ../vanguard-galaxy-tts/VGTTS/lib

# Resolve dotnet — prefer explicit local SDK, fall back to PATH
DOTNET   ?= $(shell command -v dotnet 2>/dev/null || echo /tmp/dnsdk/dotnet/dotnet)

.PHONY: all build link-asm clean deploy check-bepinex

all: build

check-bepinex:
	@test -d "$(GAME_DIR)/BepInEx/plugins" || { \
		echo "BepInEx plugins dir not found at $(GAME_DIR)/BepInEx/plugins." ; \
		echo "Install BepInEx 5.x into the game folder and launch the game once." ; \
		exit 1 ; \
	}

# Symlink the VGTTS publicized Assembly-CSharp.dll into VGTractorAuto/lib/ so we
# compile against the same stub as the other siblings (exposes private members).
link-asm:
	@mkdir -p VGTractorAuto/lib
	@if [ ! -e "VGTractorAuto/lib/Assembly-CSharp.dll" ]; then \
		ln -sf "$(abspath $(VGTTS_LIB))/Assembly-CSharp.dll" VGTractorAuto/lib/Assembly-CSharp.dll ; \
		echo "Linked Assembly-CSharp.dll from $(VGTTS_LIB)" ; \
	fi

build: link-asm
	DOTNET_ROOT=$(dir $(DOTNET)) $(DOTNET) build VGTractorAuto/VGTractorAuto.csproj -c $(CONFIG)

deploy: build check-bepinex
	@mkdir -p "$(PLUGIN_DIR)"
	cp "$(BUILDDLL)" "$(PLUGIN_DIR)/"
	@if [ -f "$(BUILDDIR)/VGTractorAuto.pdb" ]; then cp "$(BUILDDIR)/VGTractorAuto.pdb" "$(PLUGIN_DIR)/"; fi
	@echo "Deployed $(DLL) to $(PLUGIN_DIR)"

clean:
	$(DOTNET) clean VGTractorAuto/VGTractorAuto.csproj
	rm -rf VGTractorAuto/bin VGTractorAuto/obj
