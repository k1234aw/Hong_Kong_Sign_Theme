import { ModRegistrar } from "cs2/modding";
import { SignEditor } from "mods/SignEditor";

const register: ModRegistrar = (moduleRegistry) => {
  moduleRegistry.append("Game", SignEditor);
};

export default register;
