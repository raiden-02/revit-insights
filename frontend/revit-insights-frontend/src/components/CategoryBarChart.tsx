import { ResponsiveContainer, BarChart, Bar, XAxis, YAxis, Tooltip } from "recharts";
import type { Props } from "./types";


export default function CategoryBarChart({ data }: Props) {
    // console.log(data)
    if(!data?.length) return <div className="text-slate-500">No Category data available.</div>;

    return (
        <div className="w-full min-w-0">
            <div className="h-[360px] w-full min-w-0">
                <ResponsiveContainer width="100%" height="100%">
                    <BarChart data={data}>
                        <XAxis dataKey="name" hide={true}/>
                        <YAxis />
                        <Tooltip />
                        <Bar dataKey="count" />
                    </BarChart>
                </ResponsiveContainer>
            </div>
            <div className="mt-2 text-sm text-slate-500">
                {data.slice(0, 10).map(c => c.name).join(", ")}
                {data.length > 10 && " ..."}
            </div>
        </div>
    );
}