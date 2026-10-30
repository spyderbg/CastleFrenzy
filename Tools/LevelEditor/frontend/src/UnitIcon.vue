<script setup lang="ts">
import type { Unit } from "../../shared/level";
defineProps<{ unit?: Unit; assets: string }>();
</script>
<template>
  <span
    v-if="unit"
    class="unit-icon"
    :title="unit.name"
    :style="{
      backgroundImage: `url(/api/texture?path=${encodeURIComponent(assets + '/icons.png')})`,
      backgroundPosition: `-${unit.icon[0]}px -${unit.icon[1]}px`,
    }"
  ></span>
</template>
