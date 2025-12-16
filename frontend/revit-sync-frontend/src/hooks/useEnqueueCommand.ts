import { useMutation } from "@tanstack/react-query";

export type AddBoxesCommand = {
  projectName: string;
  type: "ADD_BOXES";
  boxes: Array<{
    category?: string;
    centerX: number;
    centerY: number;
    centerZ: number;
    sizeX: number;
    sizeY: number;
    sizeZ: number;
  }>;
};

export function useEnqueueCommand() {
  return useMutation({
    mutationFn: async (cmd: AddBoxesCommand) => {
      const r = await fetch("http://localhost:5245/api/commands", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(cmd),
      });
      if (!r.ok) throw new Error(await r.text());
      return r.json() as Promise<{ commandId: string }>;
    },
  });
}

